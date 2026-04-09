using System.IO;
using System.Runtime.InteropServices;
using System.Globalization;

namespace IPTVPlayer.App.Services.Media;

public sealed class MpvPlayerService : IDisposable
{
    private readonly StoragePaths _paths;
    private IntPtr _mpvHandle;
    private nint _libraryHandle;
    private bool _initialized;
    private string _availabilityMessage = "等待播放器初始化";

    public MpvPlayerService(StoragePaths paths)
    {
        _paths = paths;
    }

    public bool IsAvailable => _initialized;

    public string AvailabilityMessage => _availabilityMessage;

    public void AttachHost(IntPtr hostHandle)
    {
        if (_initialized || hostHandle == IntPtr.Zero)
        {
            return;
        }

        try
        {
            EnsureNativeLibrary();
            _mpvHandle = mpv_create();
            if (_mpvHandle == IntPtr.Zero)
            {
                _availabilityMessage = "mpv 初始化失败";
                return;
            }

            SetOption("terminal", "no");
            SetOption("input-default-bindings", "yes");
            SetOption("osc", "no");
            SetOption("force-window", "yes");
            SetOption("keep-open", "no");
            SetOption("wid", hostHandle.ToInt64().ToString());
            SetOption("hwdec", "auto-safe");
            SetOption("vo", "gpu-next");
            SetOption("gpu-api", "d3d11");
            SetOption("target-colorspace-hint", "auto");
            SetOption("tone-mapping", "auto");
            SetOption("hdr-compute-peak", "auto");

            var result = mpv_initialize(_mpvHandle);
            if (result < 0)
            {
                _availabilityMessage = $"mpv 启动失败: {GetError(result)}";
                return;
            }

            _initialized = true;
            _availabilityMessage = "mpv 已就绪";
        }
        catch (Exception ex)
        {
            _availabilityMessage = $"mpv 不可用: {ex.Message}";
        }
    }

    public bool Play(string url, out string error)
    {
        error = string.Empty;
        if (!_initialized)
        {
            error = _availabilityMessage;
            return false;
        }

        var result = RunCommand("loadfile", url, "replace");
        if (result < 0)
        {
            error = GetError(result);
            return false;
        }

        return true;
    }

    public VideoSignalInfo GetSignalInfo()
    {
        if (!_initialized)
        {
            return new VideoSignalInfo
            {
                BadgeText = "未就绪",
                DetailText = _availabilityMessage,
                DynamicRange = "未就绪",
                TechnicalSummary = _availabilityMessage
            };
        }

        var currentPath = GetProperty("path");
        if (string.IsNullOrWhiteSpace(currentPath))
        {
            return new VideoSignalInfo
            {
                BadgeText = "未播放",
                DetailText = "等待选择频道",
                DynamicRange = "未播放",
                TechnicalSummary = "等待选择频道"
            };
        }

        var width = GetIntProperty("video-params/w");
        var height = GetIntProperty("video-params/h");
        var fps = GetDoubleProperty("estimated-vf-fps") ?? GetDoubleProperty("container-fps");
        var gamma = GetProperty("video-params/gamma") ?? string.Empty;
        var primaries = GetProperty("video-params/primaries") ?? string.Empty;
        var dynamicRange = ResolveDynamicRange(gamma);
        var audioChannels = GetAudioChannelText();
        var audioCodec = GetProperty("audio-codec-name") ?? GetProperty("audio-codec");
        var audioSampleRate = GetIntProperty("audio-params/samplerate");

        var resolutionText = width.HasValue && height.HasValue ? $"{width.Value}x{height.Value}" : null;
        var frameRateText = fps.HasValue && fps.Value > 0 ? $"{fps.Value:0.##} fps" : null;
        var sampleRateText = audioSampleRate.HasValue && audioSampleRate.Value > 0
            ? $"{audioSampleRate.Value / 1000d:0.#} kHz"
            : null;

        var summaryParts = new List<string>();
        if (!string.IsNullOrWhiteSpace(resolutionText))
        {
            summaryParts.Add(resolutionText);
        }

        if (!string.IsNullOrWhiteSpace(frameRateText))
        {
            summaryParts.Add(frameRateText);
        }

        summaryParts.Add(dynamicRange);

        if (!string.IsNullOrWhiteSpace(audioChannels))
        {
            summaryParts.Add(audioChannels);
        }

        if (!string.IsNullOrWhiteSpace(audioCodec))
        {
            summaryParts.Add(audioCodec!.ToUpperInvariant());
        }

        if (!string.IsNullOrWhiteSpace(sampleRateText))
        {
            summaryParts.Add(sampleRateText);
        }

        if (!string.IsNullOrWhiteSpace(primaries) && !primaries.Equals("bt.709", StringComparison.OrdinalIgnoreCase))
        {
            summaryParts.Add(primaries);
        }

        var summary = summaryParts.Count == 0 ? "正在探测媒体信息" : string.Join(" · ", summaryParts);
        if (dynamicRange == "HLG")
        {
            return new VideoSignalInfo
            {
                BadgeText = "HLG",
                DetailText = summary,
                DynamicRange = dynamicRange,
                Resolution = resolutionText,
                FrameRate = frameRateText,
                AudioChannels = audioChannels,
                AudioCodec = audioCodec,
                AudioSampleRate = sampleRateText,
                ColorPrimaries = primaries,
                TechnicalSummary = summary
            };
        }

        if (dynamicRange == "HDR10")
        {
            return new VideoSignalInfo
            {
                BadgeText = "HDR10",
                DetailText = summary,
                DynamicRange = dynamicRange,
                Resolution = resolutionText,
                FrameRate = frameRateText,
                AudioChannels = audioChannels,
                AudioCodec = audioCodec,
                AudioSampleRate = sampleRateText,
                ColorPrimaries = primaries,
                TechnicalSummary = summary
            };
        }

        return new VideoSignalInfo
        {
            BadgeText = dynamicRange,
            DetailText = summary,
            DynamicRange = dynamicRange,
            Resolution = resolutionText,
            FrameRate = frameRateText,
            AudioChannels = audioChannels,
            AudioCodec = audioCodec,
            AudioSampleRate = sampleRateText,
            ColorPrimaries = primaries,
            TechnicalSummary = summary
        };
    }

    public void Dispose()
    {
        if (_mpvHandle != IntPtr.Zero)
        {
            mpv_destroy(_mpvHandle);
            _mpvHandle = IntPtr.Zero;
        }

        if (_libraryHandle != 0)
        {
            NativeLibrary.Free(_libraryHandle);
            _libraryHandle = 0;
        }
    }

    private void EnsureNativeLibrary()
    {
        if (_libraryHandle != 0)
        {
            return;
        }

        var candidates = new[]
        {
            Path.Combine(_paths.MpvDirectory, "mpv-2.dll"),
            Path.Combine(AppContext.BaseDirectory, "mpv-2.dll")
        };

        foreach (var candidate in candidates)
        {
            if (File.Exists(candidate))
            {
                _libraryHandle = NativeLibrary.Load(candidate);
                return;
            }
        }

        throw new FileNotFoundException("未找到内置 mpv 运行库。请确认输出目录下存在 mpv\\mpv-2.dll。");
    }

    private void SetOption(string name, string value)
    {
        var result = mpv_set_option_string(_mpvHandle, name, value);
        if (result < 0)
        {
            throw new InvalidOperationException($"{name} 设置失败: {GetError(result)}");
        }
    }

    private int RunCommand(params string[] args)
    {
        var stringPointers = new IntPtr[args.Length];
        var argvBuffer = IntPtr.Zero;
        try
        {
            for (var index = 0; index < args.Length; index++)
            {
                stringPointers[index] = Marshal.StringToHGlobalAnsi(args[index]);
            }

            argvBuffer = Marshal.AllocHGlobal(IntPtr.Size * (args.Length + 1));
            for (var index = 0; index < stringPointers.Length; index++)
            {
                Marshal.WriteIntPtr(argvBuffer, index * IntPtr.Size, stringPointers[index]);
            }

            Marshal.WriteIntPtr(argvBuffer, stringPointers.Length * IntPtr.Size, IntPtr.Zero);
            return mpv_command(_mpvHandle, argvBuffer);
        }
        finally
        {
            foreach (var pointer in stringPointers.Where(pointer => pointer != IntPtr.Zero))
            {
                Marshal.FreeHGlobal(pointer);
            }

            if (argvBuffer != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(argvBuffer);
            }
        }
    }

    private string? GetProperty(string property)
    {
        if (!_initialized)
        {
            return null;
        }

        var pointer = mpv_get_property_string(_mpvHandle, property);
        if (pointer == IntPtr.Zero)
        {
            return null;
        }

        try
        {
            return Marshal.PtrToStringAnsi(pointer);
        }
        finally
        {
            mpv_free(pointer);
        }
    }

    private int? GetIntProperty(string property)
    {
        var value = GetProperty(property);
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;
    }

    private double? GetDoubleProperty(string property)
    {
        var value = GetProperty(property);
        return double.TryParse(value, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;
    }

    private string? GetAudioChannelText()
    {
        var count = GetIntProperty("audio-params/channel-count");
        if (count.HasValue && count.Value > 0)
        {
            return $"{count.Value} ch";
        }

        var layout = GetProperty("audio-params/channels");
        return string.IsNullOrWhiteSpace(layout) ? null : layout;
    }

    private static string ResolveDynamicRange(string gamma)
    {
        if (gamma.Contains("hlg", StringComparison.OrdinalIgnoreCase))
        {
            return "HLG";
        }

        if (gamma.Contains("pq", StringComparison.OrdinalIgnoreCase) ||
            gamma.Contains("st2084", StringComparison.OrdinalIgnoreCase))
        {
            return "HDR10";
        }

        return "SDR";
    }

    private static string GetError(int errorCode)
    {
        var pointer = mpv_error_string(errorCode);
        return pointer == IntPtr.Zero ? $"错误代码 {errorCode}" : Marshal.PtrToStringAnsi(pointer) ?? $"错误代码 {errorCode}";
    }

    [DllImport("mpv-2.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr mpv_create();

    [DllImport("mpv-2.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern int mpv_initialize(IntPtr ctx);

    [DllImport("mpv-2.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern void mpv_destroy(IntPtr ctx);

    [DllImport("mpv-2.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern int mpv_set_option_string(IntPtr ctx, string name, string value);

    [DllImport("mpv-2.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern int mpv_command(IntPtr ctx, IntPtr args);

    [DllImport("mpv-2.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr mpv_get_property_string(IntPtr ctx, string name);

    [DllImport("mpv-2.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern void mpv_free(IntPtr data);

    [DllImport("mpv-2.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr mpv_error_string(int error);
}
