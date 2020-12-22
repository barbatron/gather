# gather

A Windows console executable for moving windows between screens.

## Usage

List screens/monitors
```cmd
gather ls
```

**NOTE:** Index of monitor does not necessarily correlate with device number. Click "Identify" in screen settings to see ordering.

### Move all windows from 2nd screen to primary screen
```cmd
gather from 2 to p
```

### Move all windows to 3rd screen
```cmd
gather to 3
```

### Move all consoles to primary
```cmd
gather proc conhost
```

### Usage that would never see real life use
```cmd
gather from 2,4 to 1 proc adobe
```
or 
```cmd
gather from 2 from 4 to p proc adobe,spotify,discord
```

## Building

Built as a .NET core app targetting Windows (yeah I know it could be a legacy .NET app or just a 10 line C-file).
I just hope the vscode launch/tasks are set up. Me and vscode never were good friends.

## Publishing

Use the vscode `publish` task, and copy files of output directory to wherever you need them. It's not available as a package (yet!)

Or execute this from the command line:
```
dotnet publish --runtime win-x64 --configuration Release -p:PublishSingleFile=true -p:PublishTrimmed=true --self-contained true --output "C:\your\path"
```

## Other

Contribs or suggestions welcome!