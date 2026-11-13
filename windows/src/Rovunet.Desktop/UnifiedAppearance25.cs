using System.Windows;
using System.Windows.Media;

namespace Rovunet.Desktop;

public sealed partial class MainWindow
{
    // Shared frozen brushes retain the approved palette and avoid live blur,
    // bitmap text caches or any non-uniform transform on labels.
    void ApplyUnifiedSurfaces25()
    {
        var resources=Application.Current.Resources;
        resources["MenuSurface"]=SystemParameters.HighContrast?Tone("Card"):GlassSurface("StatusSurface");
        resources["MenuEdge"]=GlassEdge(Tone("Outline"));
        resources["MenuHover"]=GlassSurface("Selected");
    }
}
