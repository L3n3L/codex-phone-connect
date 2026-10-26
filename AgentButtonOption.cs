using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace CodexPhoneConnect;

public sealed record AgentButtonOption(
    AgentDefinition Definition,
    BitmapSource Icon,
    Brush Background,
    Brush Border,
    Brush Foreground);
