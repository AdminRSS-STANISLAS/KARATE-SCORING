; Installateur Windows de KARATE SCORING (Inno Setup 6 — https://jrsoftware.org/isinfo.php).
; Empaquette la sortie de scripts/publish.ps1 (coque desktop KarateScoring.exe + serveur api/).
;
; Utilisation :
;   1. ./scripts/publish.ps1            (produit publish/win-x64/)
;   2. Ouvrir ce fichier dans Inno Setup, ou : iscc installer\setup.iss
;   Résultat : installer\output\KarateScoringSetup.exe

#define MyAppName "Karate Scoring"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "Fouda Karate Club"
#define MyAppExeName "KarateScoring.exe"
#define PublishDir "..\publish\win-x64"

[Setup]
AppId={{6C2C6F0A-6B7A-4E3E-9E8C-5B7A6C6F0A6C}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
UninstallDisplayIcon={app}\{#MyAppExeName}
OutputDir=output
OutputBaseFilename=KarateScoringSetup
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
SetupIconFile=..\src\KarateScoring.Desktop\Assets\app.ico
WizardImageFile=wizard-banner.bmp
WizardSmallImageFile=wizard-small.bmp
DisableWelcomePage=no
PrivilegesRequired=lowest

[Languages]
Name: "french"; MessagesFile: "compiler:Languages\French.isl"

[Tasks]
Name: "desktopicon"; Description: "Créer une icône sur le Bureau"; GroupDescription: "Icônes supplémentaires :"

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\Désinstaller {#MyAppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Lancer {#MyAppName}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
; Ne supprime QUE l'application — les données (base SQLite, sauvegardes, photos) vivent dans le
; dossier choisi par l'organisateur au premier lancement (voir DataFolderSettings), jamais sous {app},
; donc une désinstallation ne détruit jamais les compétitions enregistrées.
Type: filesandordirs; Name: "{app}"
