namespace FkcScoring.Core.Domain;

/// <summary>
/// Validation et stockage des images importées (photos d'athlètes, logos de clubs). Le format est
/// vérifié sur la signature binaire réelle du fichier (PNG/JPEG), pas seulement sur l'en-tête
/// Content-Type ou l'extension du nom de fichier — les deux peuvent être falsifiés ou simplement
/// faux sans mauvaise intention (un .png renommé en .jpg, par exemple).
/// </summary>
public static class ImageUploadService
{
    public const long TailleMaxOctets = 5_000_000;
    public const string FormatsAcceptes = "PNG ou JPEG, 5 Mo maximum";

    public static (bool Ok, string? Extension, string? Erreur) Valider(byte[] octets)
    {
        if (octets.Length == 0) return (false, null, "Fichier vide.");
        if (octets.Length > TailleMaxOctets) return (false, null, $"Fichier trop volumineux (5 Mo maximum, {octets.Length / 1_000_000.0:0.0} Mo reçus).");

        var estPng = octets.Length >= 8 && octets[0] == 0x89 && octets[1] == 0x50 && octets[2] == 0x4E && octets[3] == 0x47;
        var estJpeg = octets.Length >= 3 && octets[0] == 0xFF && octets[1] == 0xD8 && octets[2] == 0xFF;
        if (!estPng && !estJpeg)
            return (false, null, $"Format de fichier non reconnu. Formats acceptés : {FormatsAcceptes}.");

        return (true, estPng ? "png" : "jpg", null);
    }

    public static string ContentType(string extension) => extension == "png" ? "image/png" : "image/jpeg";

    public static string CheminFichier(string uploadsDir, string prefixe, int id, string extension) =>
        Path.Combine(uploadsDir, $"{prefixe}-{id}.{extension}");

    /// <summary>Enregistre le fichier et supprime l'ancienne image si son extension différait (ex. remplacement d'un .png par un .jpg).</summary>
    public static void Enregistrer(string uploadsDir, string prefixe, int id, string extension, byte[] octets, string? ancienneExtension)
    {
        Directory.CreateDirectory(uploadsDir);
        if (ancienneExtension != null && ancienneExtension != extension)
        {
            var ancien = CheminFichier(uploadsDir, prefixe, id, ancienneExtension);
            if (File.Exists(ancien)) File.Delete(ancien);
        }
        File.WriteAllBytes(CheminFichier(uploadsDir, prefixe, id, extension), octets);
    }

    public static void Supprimer(string uploadsDir, string prefixe, int id, string extension)
    {
        var chemin = CheminFichier(uploadsDir, prefixe, id, extension);
        if (File.Exists(chemin)) File.Delete(chemin);
    }
}
