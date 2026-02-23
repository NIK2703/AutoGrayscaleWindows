# Auto Grayscale Windows

A lightweight WPF application that automatically toggles Windows color filters based on the active window. Perfect for reducing eye strain or creating a distraction-free workflow by automatically switching between grayscale and color modes.

## Features

- **Automatic Mode Switching** — Toggles grayscale filter based on the active application window
- **Whitelist/Blacklist Modes** — Configure which apps should trigger grayscale or stay in color
- **Flexible Matching Rules** — Match apps by executable path, or window title with support for exact, substring, or regex patterns

- Windows 10 version 1703 (build 15063) or later
- [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0)

## How It Works

The application monitors the active window and evaluates user-defined rules to determine whether to enable or disable the Windows color filter. It uses the built-in Windows color filter feature (the same one toggled by `Win+Ctrl+C`) and controls it via registry and keyboard simulation.

### Rule Modes

| Mode | Behavior |
|------|----------|
| **Blacklist** (default) | Apps in the list stay in color; everything else is grayscale |
| **Whitelist** | Apps in the list become grayscale; everything else stays in color |

### Rule Matching

Rules can match applications by:
- **Executable path** — e.g., `C:\Program Files\Adobe\Photoshop\photoshop.exe`
- **Window title** — e.g., match specific browser tabs or document windows

## Building

```bash
git clone https://github.com/your-username/auto-grayscale-windows.git
cd auto-grayscale-windows
dotnet build
```

## License

MIT License — feel free to use, modify, and distribute.