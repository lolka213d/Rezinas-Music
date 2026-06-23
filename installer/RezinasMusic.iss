; Rezinas Music — Windows installer (Inno Setup 6)
; Build: .\installer\build-installer.ps1

#define MyAppName "Rezinas Music"
#define MyAppExe "RezinasMusic.exe"
#define MyAppVersion "1.2.2"
#define MyAppPublisher "Rezinas"
#define MyAppUrl "https://github.com/lolka213d/Rezinas-Music"

[Setup]
AppId={{A4B8C2E1-9F3D-4A6B-8C1E-2D5F7A9B3E4C}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppUrl}
AppSupportURL={#MyAppUrl}
AppUpdatesURL={#MyAppUrl}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
AllowNoIcons=yes
OutputDir=..\publish
OutputBaseFilename=RezinasMusic-Setup-{#MyAppVersion}
SetupIconFile=..\src\Harmony\Assets\app-icon.ico
UninstallDisplayIcon={app}\{#MyAppExe}
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
DisableProgramGroupPage=yes
ShowLanguageDialog=yes
LanguageDetectionMethod=uilanguage

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "russian"; MessagesFile: "compiler:Languages\Russian.isl"

[CustomMessages]
english.AppLanguageTitle=Application language
english.AppLanguageDescription=Choose the default interface language for Rezinas Music. You can change it later in Settings.
english.AppLangEnglish=English
english.AppLangRussian=Russian
english.AppLangUkrainian=Ukrainian
english.AppLangSpanish=Spanish
english.AppLangGerman=German
english.AppLangFrench=French
english.AppLangItalian=Italian
english.AppLangPortuguese=Portuguese
english.AppLangPolish=Polish
english.AppLangJapanese=Japanese

russian.AppLanguageTitle=Язык приложения
russian.AppLanguageDescription=Выберите язык интерфейса Rezinas Music. Позже его можно изменить в настройках.
russian.AppLangEnglish=Английский
russian.AppLangRussian=Русский
russian.AppLangUkrainian=Украинский
russian.AppLangSpanish=Испанский
russian.AppLangGerman=Немецкий
russian.AppLangFrench=Французский
russian.AppLangItalian=Итальянский
russian.AppLangPortuguese=Португальский
russian.AppLangPolish=Польский
russian.AppLangJapanese=Японский

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "..\publish\win-x64\{#MyAppExe}"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\publish\win-x64\Assets\*"; DestDir: "{app}\Assets"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExe}"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExe}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExe}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[Code]
var
  LangPage: TWizardPage;
  LangCombo: TNewComboBox;

const
  LangCount = 10;

function GetLangCode(Index: Integer): String;
begin
  case Index of
    0: Result := 'en';
    1: Result := 'ru';
    2: Result := 'uk';
    3: Result := 'es';
    4: Result := 'de';
    5: Result := 'fr';
    6: Result := 'it';
    7: Result := 'pt';
    8: Result := 'pl';
    9: Result := 'ja';
  else
    Result := 'en';
  end;
end;

procedure InitializeWizard;
begin
  LangPage := CreateCustomPage(
    wpSelectDir,
    ExpandConstant('{cm:AppLanguageTitle}'),
    ExpandConstant('{cm:AppLanguageDescription}'));

  LangCombo := TNewComboBox.Create(LangPage);
  LangCombo.Parent := LangPage.Surface;
  LangCombo.Left := 0;
  LangCombo.Top := ScaleY(16);
  LangCombo.Width := LangPage.SurfaceWidth;
  LangCombo.Height := ScaleY(23);
  LangCombo.Style := csDropDownList;

  LangCombo.Items.Add(ExpandConstant('{cm:AppLangEnglish}'));
  LangCombo.Items.Add(ExpandConstant('{cm:AppLangRussian}'));
  LangCombo.Items.Add(ExpandConstant('{cm:AppLangUkrainian}'));
  LangCombo.Items.Add(ExpandConstant('{cm:AppLangSpanish}'));
  LangCombo.Items.Add(ExpandConstant('{cm:AppLangGerman}'));
  LangCombo.Items.Add(ExpandConstant('{cm:AppLangFrench}'));
  LangCombo.Items.Add(ExpandConstant('{cm:AppLangItalian}'));
  LangCombo.Items.Add(ExpandConstant('{cm:AppLangPortuguese}'));
  LangCombo.Items.Add(ExpandConstant('{cm:AppLangPolish}'));
  LangCombo.Items.Add(ExpandConstant('{cm:AppLangJapanese}'));
  LangCombo.ItemIndex := 0;
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  LangCode: String;
  LangPath: String;
begin
  if CurStep = ssPostInstall then
  begin
    if LangCombo.ItemIndex < 0 then
      LangCombo.ItemIndex := 0;
    LangCode := GetLangCode(LangCombo.ItemIndex);
    LangPath := ExpandConstant('{app}\install.lang');
    SaveStringToFile(LangPath, LangCode, False);
  end;
end;
