"""Prepare a temporary validation-player capture path, preserving delivered sources."""
from pathlib import Path

root = Path(__file__).resolve().parents[1]
clone = root / 'Builds/ValidationProject'
backup = root / 'Builds/PortfolioCaptureSourceBackup'
backup.mkdir(parents=True, exist_ok=True)
relative_runner = Path('Assets/PocketToyworks/Runtime/VerificationRunner.cs')
relative_setup = Path('Assets/PocketToyworks/Editor/WorkshopSetup.cs')
for relative in [relative_runner, relative_setup]:
    original = root / relative
    (backup / relative.name).write_bytes(original.read_bytes())
    (clone / relative).write_bytes(original.read_bytes())
runner = (clone / relative_runner).read_text(encoding='utf-8-sig')
shots = (root / 'Documentation/Upwork-Screenshots').as_posix()
runner = runner.replace('const string Shots="Verification/Screenshots/v0.5.0";',
    f'static readonly string Shots=Environment.GetCommandLineArgs().Contains("--portfolio-workshop")?"{shots}":"Verification/Screenshots/v0.5.0";')
runner = runner.replace('view.SetVerificationFocus(true);',
    'view.SetVerificationFocus(true);\n            if(Environment.GetCommandLineArgs().Contains("--portfolio-workshop")){yield return Portfolio(view);yield break;}', 1)
portfolio = '''
        IEnumerator Portfolio(WorkshopView view)
        {
            captureScale=2;Screen.SetResolution(540,960,FullScreenMode.Windowed);yield return new WaitForSeconds(.8f);
            if(Screen.width!=540||Screen.height!=960){Fail("Portfolio framebuffer mismatch");yield break;}
            view.SetVerificationSafeArea(new Rect(0,0,540,960));
            view.Data.completed=Enumerable.Range(1,6).Select(n=>"level-"+n.ToString("00")).ToList();
            view.Data.pausedAttempts.Clear();
            view.Data.player=new PlayerProgress{initialized=true,playerName="Pocket Maker",coins=450,totalEarned=300,challengeWins=6,avatar=7,frame=2,ownedFrames=new List<int>{0,1,2},rewardedLevels=new List<string>(view.Data.completed)};
            Progression.Ensure(view.Data,DateTime.UtcNow);
            view.LoadLevel("level-07");
            foreach(int index in new[]{0,1,2,3,5,6}){if(!view.Commit(view.Level.solution[index])){Fail("Portfolio trace rejected");yield break;}yield return null;}
            view.Data.player.remainingSeconds=216;yield return new WaitForSeconds(.3f);yield return Capture("01-gameplay-dual");
            view.EnterHome();yield return Capture("02-level-trail");
            yield return ClickArt("avatar-7");yield return ClickNamed("Characters");yield return Capture("03-character-customization");yield return ClickNamed("Done");
            yield return ClickArt("icon-plus",true);yield return Capture("04-workshop-rewards");
            view.LoadLevel("level-07");foreach(var command in view.Level.solution){if(!view.Commit(command)){Fail("Portfolio result trace rejected");yield break;}yield return null;}
            yield return new WaitForSeconds(1.5f);yield return Capture("05-toy-complete");
            view.LoadLevel("level-04");foreach(var command in view.Level.solution.Take(2)){if(!view.Commit(command)){Fail("Portfolio single trace rejected");yield break;}yield return null;}
            view.Data.player.remainingSeconds=168;yield return Capture("06-gameplay-single");
            yield return ClickNamed("Learn");yield return Capture("07-guided-tutorial");
            File.WriteAllText(Path.Combine(Shots,"capture-report.txt"),"Actual Unity 0.5.0 UI rendered to seven 1080x1920 PNGs from a verified 540x960 framebuffer using a native camera canvas. Demonstration save fixtures and legal authored moves; no image retouching. Runtime errors="+errors+"; geometry failure="+layoutFailed+". Desktop captures, not Android device photographs.");
            Application.Quit(errors==0&&!layoutFailed?0:1);
        }
'''
runner = runner.replace('        Vector2 ScreenPoint', portfolio + '\n        Vector2 ScreenPoint', 1)
(clone / relative_runner).write_text(runner, encoding='utf-8')
setup = (clone / relative_setup).read_text(encoding='utf-8-sig').replace('locationPathName="Builds/Windows/PocketToyworks.exe"', 'locationPathName="Builds/PortfolioWindows/PocketToyworks.exe"').replace('File.WriteAllText("Verification/windows-build.txt"', 'File.WriteAllText("Verification/portfolio-windows-build.txt"')
(clone / relative_setup).write_text(setup, encoding='utf-8')
(root / 'Builds/PortfolioCaptureSession').mkdir(parents=True, exist_ok=True)
print('Temporary capture runner prepared. Restore both clone source files after building.')
