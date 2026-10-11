using System.Windows.Forms;
using System.Drawing;
// Compile with the Windows .NET Framework C# compiler. The installer itself
// needs only the .NET Framework shipped with Windows 10/11, while the app is self-contained.
using System;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;

static class Installer
{
    const string RegistryPath = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\VelvetVoice";
    static string InstallRoot { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "VelvetVoice"); } }
    static string DesktopLink { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "Velvet Audio.lnk"); } }
    static string MenuLink { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "Velvet Audio.lnk"); } }
    static Assembly Assembly { get { return typeof(Installer).Assembly; } }
    [STAThread]
    static int Main(string[] args)
    {
        try
        {
            if (args.Length > 0 && args[0] == "--verify-payload") { Verify(); return 0; }
            if (args.Length > 0 && args[0] == "--preview") { Application.EnableVisualStyles(); ShowInstaller(true); return 0; }
            if (args.Length == 2 && args[0] == "--test-install") { Extract(Path.GetFullPath(args[1])); return 0; }
            if (args.Length > 0 && args[0] == "--install") { Install(); return 0; }
            if (args.Length == 2 && args[0] == "--remove") { Remove(int.Parse(args[1])); return 0; }
            Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
            if ((args.Length > 0 && args[0] == "--uninstall") || Assembly.GetManifestResourceInfo("payload.zip") == null)
            { BeginUninstall(); return 0; }
            ShowInstaller(false); return 0;
        }
        catch (Exception ex)
        {
            if (args.Length == 0) MessageBox.Show(ex.Message, "Velvet Audio setup", MessageBoxButtons.OK, MessageBoxIcon.Error);
            else File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "setup-error.txt"), ex.ToString());
            return 1;
        }
    }
    static string Hash(Stream stream)
    {
        using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
    }
    static void Verify()
    {
        string actual = null, expected = null;
        using (var stream = Assembly.GetManifestResourceStream("payload.zip"))
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Read))
        {
            foreach (var entry in zip.Entries)
            {
                if (entry.FullName != Path.GetFileName(entry.FullName) || entry.FullName.Contains("\\")) throw new InvalidDataException("Unexpected payload path.");
                using (var file = entry.Open())
                {
                    if (entry.FullName == "VelvetVoice.exe") actual = Hash(file);
                    else if (entry.FullName == "app.sha256") using (var reader = new StreamReader(file)) expected = reader.ReadToEnd().Trim();
                    else { var block = new byte[65536]; while (file.Read(block, 0, block.Length) != 0) { } }
                }
            }
        }
        if (actual == null || actual != expected) throw new InvalidDataException("Application payload checksum failed.");
        File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "setup-verification.txt"), "PASS payload decompression and application SHA-256: " + actual);
    }
    static void Extract(string root)
    {
        root = Path.GetFullPath(root);
        if (Directory.Exists(root) && (File.GetAttributes(root) & FileAttributes.ReparsePoint) != 0) throw new IOException("Install directory cannot be a link.");
        Directory.CreateDirectory(root);
        using (var stream = Assembly.GetManifestResourceStream("payload.zip"))
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Read))
        {
            foreach (var entry in zip.Entries)
            {
                if (entry.FullName != Path.GetFileName(entry.FullName) || entry.FullName.Contains("\\")) throw new InvalidDataException("Unexpected payload path.");
                var destination = Path.GetFullPath(Path.Combine(root, entry.FullName));
                if (!destination.StartsWith(root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Invalid payload path.");
                if (File.Exists(destination) && (File.GetAttributes(destination) & FileAttributes.ReparsePoint) != 0) throw new IOException("Install file cannot be a link.");
                using (var source = entry.Open()) using (var target = File.Create(destination)) source.CopyTo(target);
            }
        }
        using (var app = File.OpenRead(Path.Combine(root, "VelvetVoice.exe")))
            if (Hash(app) != File.ReadAllText(Path.Combine(root, "app.sha256")).Trim()) throw new InvalidDataException("Installed application checksum failed.");
    }
    static void Install()
    {
        foreach (var p in Process.GetProcessesByName("VelvetVoice"))
        {
            using (p)
            {
                try { if (p.MainModule.FileName.StartsWith(InstallRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new IOException("Close Velvet Audio before updating it."); }
                catch (System.ComponentModel.Win32Exception) { }
            }
        }
        Extract(InstallRoot);
        CleanupLegacyShortcuts();
        Shortcut(DesktopLink, false); Shortcut(MenuLink, false);
        using (var key = Registry.CurrentUser.CreateSubKey(RegistryPath))
        {
            key.SetValue("DisplayName", "Velvet Audio"); key.SetValue("DisplayVersion", "1.4.0");
            key.SetValue("InstallLocation", InstallRoot); key.SetValue("DisplayIcon", Path.Combine(InstallRoot, "VelvetVoice.exe"));
            key.SetValue("UninstallString", "\"" + Path.Combine(InstallRoot, "Uninstall.exe") + "\" --uninstall");
            key.SetValue("NoModify", 1, RegistryValueKind.DWord); key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
        }
    }
    static void Shortcut(string path, bool remove)
    {
        if (remove && !File.Exists(path)) return;
        object shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell"));
        object shortcut = null;
        try
        {
            shortcut = shell.GetType().InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell, new object[] { path });
            string target = Path.Combine(InstallRoot, "VelvetVoice.exe");
            if (remove)
            {
                string existing = (string)shortcut.GetType().InvokeMember("TargetPath", BindingFlags.GetProperty, null, shortcut, null);
                if (string.Equals(existing, target, StringComparison.OrdinalIgnoreCase)) File.Delete(path);
                return;
            }
            foreach (var property in new[] { new[] { "TargetPath", target }, new[] { "WorkingDirectory", InstallRoot }, new[] { "IconLocation", target + ",0" }, new[] { "Description", "Velvet Audio microphone processor" } })
                shortcut.GetType().InvokeMember(property[0], BindingFlags.SetProperty, null, shortcut, new object[] { property[1] });
            shortcut.GetType().InvokeMember("Save", BindingFlags.InvokeMethod, null, shortcut, null);
        }
        finally { if (shortcut != null) Marshal.FinalReleaseComObject(shortcut); Marshal.FinalReleaseComObject(shell); }
    }
    static void ShowInstaller(bool preview)
    {
        using (var form = new Form())
        {
            form.Text = "Velvet Audio setup"; form.ClientSize = new Size(590, 380); form.FormBorderStyle = FormBorderStyle.FixedDialog;
            form.MaximizeBox = false; form.StartPosition = FormStartPosition.CenterScreen; form.Font = new Font("Segoe UI", 10);
            form.BackColor = Color.FromArgb(16, 23, 32); form.ForeColor = Color.FromArgb(224, 232, 238);
            form.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            using (var stream = Assembly.GetManifestResourceStream("logo.png"))
            using (var image = Image.FromStream(stream))
                form.Controls.Add(new PictureBox { Image = new Bitmap(image), SizeMode = PictureBoxSizeMode.Zoom, Bounds = new Rectangle(24, 22, 78, 78) });
            form.Controls.Add(new Label { Text = "VELVET AUDIO", Font = new Font("Segoe UI", 20, FontStyle.Bold), Bounds = new Rectangle(118, 23, 430, 40) });
            form.Controls.Add(new Label { Text = "Broadcast character. Every whisper intact.", Bounds = new Rectangle(120, 66, 430, 28) });
            form.Controls.Add(new Label { Text = "Install the desktop app for your Windows account.\nStudio processing, soundboard, instant clips, and voice effects.\nIncludes the runtime and desktop / Start menu shortcuts.\nVoiceMeeter and VB-CABLE are separate audio drivers.\n\nInstallation folder:\n" + InstallRoot, Bounds = new Rectangle(26, 120, 540, 145) });
            var launch = new CheckBox { Text = "Open Velvet Audio after installation", Checked = true, Bounds = new Rectangle(26, 278, 470, 28) }; form.Controls.Add(launch);
            var status = new Label { Bounds = new Rectangle(26, 342, 535, 28) }; form.Controls.Add(status);
            var button = new Button { Text = "Install", Bounds = new Rectangle(405, 302, 155, 36), FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(46, 133, 121), ForeColor = Color.White }; form.Controls.Add(button);
            bool installed = false;
            button.Click += delegate
            {
                if (installed) { form.Close(); return; }
                button.Enabled = false; status.Text = "Installing…"; form.Refresh();
                try
                {
                    Install(); status.Text = "Installed. Your desktop shortcut is ready.";
                    if (launch.Checked) Process.Start(new ProcessStartInfo(Path.Combine(InstallRoot, "VelvetVoice.exe")) { UseShellExecute = true });
                    installed = true; button.Text = "Close"; button.Enabled = true;
                }
                catch (Exception ex) { status.Text = "Installation failed."; button.Enabled = true; MessageBox.Show(form, ex.Message, "Setup", MessageBoxButtons.OK, MessageBoxIcon.Error); }
            };
            if (preview)
            {
                form.Show(); Application.DoEvents();
                using (var bitmap = new Bitmap(form.Width, form.Height))
                { form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, form.Size)); bitmap.Save(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "setup-preview.png")); }
            }
            else Application.Run(form);
        }
    }
    static void BeginUninstall()
    {
        if (MessageBox.Show("Remove Velvet Audio and its shortcuts?\nYour saved voice settings will be retained. Close the app first.", "Uninstall Velvet Audio", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
        var worker = Path.Combine(Path.GetTempPath(), "VelvetVoice-Uninstall-" + Guid.NewGuid().ToString("N") + ".exe");
        File.Copy(Application.ExecutablePath, worker);
        Process.Start(new ProcessStartInfo(worker, "--remove " + Process.GetCurrentProcess().Id) { UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden });
    }
    static void CleanupLegacyShortcuts()
    {
        Shortcut(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "Velvet Voice.lnk"), true);
        Shortcut(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "Velvet Voice.lnk"), true);
    }
    static void Remove(int parentId)
    {
        try { using (var parent = Process.GetProcessById(parentId)) parent.WaitForExit(10000); } catch (ArgumentException) { }
        string root = Path.GetFullPath(InstallRoot);
        string expected = Path.GetFullPath(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "VelvetVoice"));
        if (!string.Equals(root, expected, StringComparison.OrdinalIgnoreCase)) throw new IOException("Unexpected uninstall directory.");
        if (Directory.Exists(root) && (File.GetAttributes(root) & FileAttributes.ReparsePoint) != 0) throw new IOException("Uninstall directory cannot be a link.");
        CleanupLegacyShortcuts();
        Shortcut(DesktopLink, true); Shortcut(MenuLink, true);
        if (Directory.Exists(root))
        {
            foreach (var name in new[] { "VelvetVoice.exe", "rnnoise.dll", "silero_vad.onnx", "Uninstall.exe", "README.md", "THIRD-PARTY.txt", "app.sha256" })
            {
                var file = Path.Combine(root, name);
                if (File.Exists(file) && (File.GetAttributes(file) & FileAttributes.ReparsePoint) == 0) File.Delete(file);
            }
            // Do not recurse into user-created directories or links.
            if (Directory.GetFileSystemEntries(root).Length == 0) Directory.Delete(root, false);
        }
        Registry.CurrentUser.DeleteSubKeyTree(RegistryPath, false);
    }
}
