using System.Windows;

namespace FkcScoring.App.Services;

/// <summary>Détection du second écran (TV branchée en HDMI) pour y positionner l'écran de diffusion publique.</summary>
public static class EcranService
{
    public static bool SecondEcranDisponible => System.Windows.Forms.Screen.AllScreens.Length > 1;

    /// <summary>Positionne la fenêtre en plein écran sans bordure sur le second moniteur si disponible, sinon sur le principal.</summary>
    public static void PlacerSurSecondEcran(Window fenetre)
    {
        var ecran = System.Windows.Forms.Screen.AllScreens.FirstOrDefault(s => !s.Primary) ?? System.Windows.Forms.Screen.PrimaryScreen!;
        var zone = ecran.Bounds;

        fenetre.WindowStyle = WindowStyle.None;
        fenetre.ResizeMode = ResizeMode.NoResize;
        fenetre.WindowStartupLocation = WindowStartupLocation.Manual;
        fenetre.Left = zone.Left;
        fenetre.Top = zone.Top;
        fenetre.Width = zone.Width;
        fenetre.Height = zone.Height;
        fenetre.Topmost = true;
        fenetre.WindowState = WindowState.Normal;
    }
}
