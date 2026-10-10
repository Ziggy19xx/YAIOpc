# Publish this project to GitHub

The workspace has a Git repository connected to `https://github.com/Ziggy19xx/YAIOpc.git`. At preparation time, there were no commits, and the project files were staged. Run the commands below in PowerShell.

## 1. Create the GitHub repository

On GitHub, create a repository such as `framepilot-velvetvoice`. Start private while reviewing the project's license, third-party redistribution rights, and assets. Leave the options to add a README, license, or .gitignore unchecked because the local project already includes setup files.

Copy the repository's HTTPS URL.

## 2. Create the initial commit

```powershell
Set-Location 'C:\Users\Ziggy\Documents\ChatGPT\performance app 2'
git status --short
git add .gitignore .gitattributes README.md GITHUB-SETUP.md .github CombinedStudio-source HANDOFF.md
git diff --cached --stat
git commit -m "Initial FramePilot and Velvet Voice combined application"
git branch -M main
```

If Git asks for your identity, set your name and GitHub email for this repository, then repeat the commit:

```powershell
git config user.name "YOUR NAME"
git config user.email "YOUR GITHUB EMAIL OR NOREPLY ADDRESS"
```

## 3. Connect and push

Replace `YOUR-USERNAME` and the repository name with the URL you copied:

```powershell
git remote set-url origin https://github.com/Ziggy19xx/YAIOpc.git
git push -u origin main
```

Complete the browser sign-in if Git prompts. If `origin` already exists, inspect `git remote -v` and use `git remote set-url origin YOUR-REPOSITORY-URL` instead of adding another remote.

## 4. Check the build

Open the repository's Actions tab and select **Windows build**. Once the job succeeds, download **FramePilot-VelvetVoice-win-x64** from its artifacts. Extract the entire archive and run `FramePilot.exe`.

For a local build, install the .NET 8 SDK for Windows and follow the root README. At preparation time, this machine's `dotnet` command had no SDK installed, and `gh` was unavailable; neither is needed to push using Git.

## 5. Subsequent updates

```powershell
git status --short
git add CombinedStudio-source README.md GITHUB-SETUP.md .github .gitignore .gitattributes HANDOFF.md
git diff --cached --stat
git commit -m "Describe your change"
git push
```

Generated `bin`, `obj`, output distributions, logs, Python caches, and earlier source copies are ignored. Bundled runtimes, native libraries, and models are intentionally included because the build depends on them. Keep credentials, private recordings, and personal settings out of commits. Distribute complete published ZIPs through GitHub Releases, rather than committing build output.
