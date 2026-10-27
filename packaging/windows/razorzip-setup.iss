; RazorZip installer (Inno Setup).
;
; SCOPE AND HONESTY NOTICE (read before building):
; - Default build (no defines): installs the OPEN benchmark harness only
;   ("Core" features) plus the trial-request workflow. No commercial engine,
;   no license keys, no credentials are bundled.
; - Commercial engine files are compiled in ONLY when ALL of the following hold,
;   enforced by scripts/build-installers.ps1 against the in-tree
;   approved-binaries.txt (currently: razor-train.exe + sidecar only):
;     1. ISCC is invoked with /DCOMMERCIAL, AND
;     2. every staged commercial artifact is named in approved-binaries.txt, AND
;     3. each staged hash equals the allow-list hash and the detached
;        .integrity sidecar digest.
;   Without /DCOMMERCIAL the commercial [Files]/[Icons] sections below are
;   excluded at compile time. The installer never fabricates binaries.
;   Formerly quarantined binaries (bench/watch/decode, see
;   artifacts/hardened/MANIFEST.txt) are verified fixed but await the
;   protector wrap — NOT referenced here yet. ISCC fails closed if a
;   listed file is absent (no skip flags).
; - The trial page NEVER sends email: it opens the user's own email app with
;   a prefilled message (or offers copy-to-clipboard). Silent installs skip
;   the trial page; use the installed GUI/CLI afterwards.
;
; Build (harness-only):  iscc packaging\windows\razorzip-setup.iss /DHarnessDir=<dotnet publish dir>
; Build (commercial):    only via scripts/build-installers.ps1 (gated).

#define AppName "RazorZip"
#define AppVersion "0.1.0"
#define AppPublisher "Mobisec Cloud"
#define AppURL "https://github.com/mobiseccloud/Razor-Public"
#ifndef HarnessDir
  #define HarnessDir "..\..\staging\harness"
#endif

[Setup]
AppId={{8C1F4E2A-7B3D-4A9E-9F0C-2D5A6B7C8D9E0}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppURL}
AppSupportURL={#AppURL}
DefaultDirName={commonpf}\MobiSec\RazorZip
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
; Per-user installs are supported via the privileges dialog above.
OutputDir=..\..\dist
OutputBaseFilename=RazorZip-{#AppVersion}-setup
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
UninstallDisplayName={#AppName} {#AppVersion} (open harness)
DisableProgramGroupPage=no

[Types]
Name: "full"; Description: "Full installation (recommended)"
Name: "harness"; Description: "Benchmark harness only (Core)"
Name: "cli"; Description: "CLI and headless tools only"
Name: "custom"; Description: "Custom"; Flags: iscustom

[Components]
Name: "harness"; Description: "Open benchmark GUI harness (Core)"; Types: full harness custom
Name: "cli"; Description: "CLI helpers (headless trial-template print)"; Types: full cli custom
#ifdef COMMERCIAL
Name: "engine"; Description: "Commercial Razor engine (allow-listed binaries only — currently razor-train)"; Types: full custom
Name: "watcher"; Description: "Watcher service files (reserved for a qualified watcher build; installs nothing in this release)"; Types: full custom
#endif

[Files]
; Open harness payload, staged by scripts/build-installers.ps1 (dotnet publish).
Source: "{#HarnessDir}\*"; DestDir: "{app}\harness"; Flags: ignoreversion recursesubdirs createallsubdirs; Components: harness cli
; Trial template source of truth, installed for copy-fallback and manual use.
Source: "..\..\docs\trial\trial-request-template.txt"; DestDir: "{app}"; Flags: ignoreversion; Components: harness cli
#ifdef COMMERCIAL
; Allow-listed commercial payload ONLY (currently: razor-train.exe).
; Formerly quarantined bench/watch/decode are verified fixed (see
; artifacts/hardened/MANIFEST.txt) but await the protector wrap before
; distribution — they are deliberately NOT referenced here yet.
; ISCC fails closed here if a listed file is absent (no skip flags).
Source: "..\..\artifacts\hardened\razor-train.exe"; DestDir: "{app}\engine"; Components: engine
Source: "..\..\artifacts\hardened\razor-train.exe.integrity"; DestDir: "{app}\engine"; Components: engine
#endif

[Icons]
Name: "{group}\Razor Benchmark Harness"; Filename: "{app}\harness\RazorBenchmark.exe"; Components: harness
Name: "{group}\Request 14-Day Trial"; Filename: "{app}\harness\RazorBenchmark.exe"; Parameters: "--request-trial"; Components: harness
Name: "{commondesktop}\Razor Benchmark"; Filename: "{app}\harness\RazorBenchmark.exe"; Components: harness; Tasks: desktopicon
#ifdef COMMERCIAL
Name: "{group}\Razor Trainer (commercial)"; Filename: "{app}\engine\razor-train.exe"; Components: engine
#endif

[Tasks]
Name: desktopicon; Description: "Create a desktop shortcut"; Flags: unchecked

[Code]
var
  TrialPage: TWizardPage;
  TrialInfo: TNewStaticText;
  RequestBtn, CopyBtn: TNewButton;

const
  // Canonical prefilled request (UTF-8 percent-encoded). Generated from
  // docs/trial/trial-request-template.txt; see packaging/shared/trial-mailto.txt.
  // 868 chars — within safe handler limits; never truncated by this installer.
  TrialMailto = 'mailto:razor%40mobisec.cloud?subject=RazorZip%2014-Day%20Enterprise%20Trial%20Request&body=Hello%20MobiSec%20Team%2C%0A%0A%0AI%20would%20like%20to%20request%20a%2014-day%20RazorZip%20Enterprise%20trial.%0A%0A%0A---%20BEGIN%20TRIAL%20REQUEST%20---%0AEmail%20Address%3A%20%5BYour%20Email%20Address%5D%0AGiven%20Name%3A%20%5BYour%20First%20Name%5D%0ALast%20Name%3A%20%5BYour%20Last%20Name%5D%0ACell%20Number%3A%20%5BYour%20Phone%20Number%5D%0ABusiness%20Name%3A%20%5BCompany%20Name%20or%20write%20%22PRIVATE%22%5D%0AHeard%20About%20From%3A%20%5Be.g.%2C%20GitHub%2C%20Hacker%20News%2C%20Colleague%5D%0ACountry%3A%20%5BYour%20Country%5D%0ATown%2FCity%3A%20%5BYour%20City%5D%0A---%20END%20TRIAL%20REQUEST%20---%0A%0A%0APlease%20let%20me%20know%20the%20next%20steps%20for%20obtaining%20and%20activating%20my%20trial%20license.%0A%0A%0AKind%20regards%2C%0A%5BYour%20Name%5D%0A';

procedure RequestTrialByEmail(Sender: TObject);
var
  Err: Integer;
begin
  // Opens the user's REGISTERED default email app. Nothing is sent by us.
  if ShellExec('open', TrialMailto, '', '', SW_SHOW, ewNoWait, Err) then
    MsgBox('Your email app should now show a prefilled trial request.' + #13#10 +
           'Review it and press Send yourself — nothing has been sent automatically.',
           mbInformation, MB_OK)
  else
    MsgBox('No email application is registered to handle email links (code ' +
           IntToStr(Err) + ').' + #13#10 +
           'Use "Copy Request Template" and paste it into an email to razor@mobisec.cloud yourself.',
           mbError, MB_OK);
end;

procedure CopyTrialTemplate(Sender: TObject);
var
  TemplatePath, PowerShellCmd: String;
  ResultCode: Integer;
begin
  TemplatePath := ExpandConstant('{app}\trial-request-template.txt');
  // Clipboard fallback via PowerShell; never touches mail credentials.
  // (Set-Clipboard takes -Value from the pipeline; -LiteralPath belongs to Get-Content.)
  PowerShellCmd := '-NoProfile -Command "Get-Content -Raw -LiteralPath ''' + TemplatePath + ''' | Set-Clipboard"';
  if Exec('powershell.exe', PowerShellCmd, '', SW_HIDE, ewWaitUntilTerminated, ResultCode)
     and (ResultCode = 0) then
    MsgBox('Request template copied. Paste it into an email to razor@mobisec.cloud and send it yourself.',
           mbInformation, MB_OK)
  else
    MsgBox('Copy failed. Open ' + TemplatePath + ' and copy the text manually.',
           mbError, MB_OK);
end;

procedure InitializeWizard;
begin
  TrialPage := CreateCustomPage(wpSelectTasks,
    'Request Your 14-Day RazorZip Enterprise Trial',
    'A trial request is submitted by email; a license is provided separately after review. ' +
    'This step never sends anything — it only opens your own email app, prefilled.');
  TrialInfo := TNewStaticText.Create(TrialPage);
  TrialInfo.Parent := TrialPage.Surface;
  TrialInfo.Top := 8; TrialInfo.Left := 0;
  TrialInfo.Width := TrialPage.SurfaceWidth;
  TrialInfo.AutoSize := True;
  TrialInfo.Caption := 'Core (harness) features work without a license. ' +
    'Pro/Enterprise engine features require a trial or paid license.';

  RequestBtn := TNewButton.Create(TrialPage);
  RequestBtn.Parent := TrialPage.Surface;
  RequestBtn.Top := 64; RequestBtn.Left := 0;
  RequestBtn.Width := 220; RequestBtn.Height := 32;
  RequestBtn.Caption := 'Request Trial by Email';
  RequestBtn.OnClick := @RequestTrialByEmail;

  CopyBtn := TNewButton.Create(TrialPage);
  CopyBtn.Parent := TrialPage.Surface;
  CopyBtn.Top := 64; CopyBtn.Left := 232;
  CopyBtn.Width := 220; CopyBtn.Height := 32;
  CopyBtn.Caption := 'Copy Request Template';
  CopyBtn.OnClick := @CopyTrialTemplate;
end;

function ShouldSkipPage(PageID: Integer): Boolean;
begin
  // Silent installs are non-interactive: skip the trial page there.
  // Use the installed GUI ("Request 14-Day Trial" tab) or --request-trial afterwards.
  Result := (PageID = TrialPage.ID) and WizardSilent;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  // Uninstall removes program files only; user data and any license files
  // outside {app} are never touched by this installer.
end;
