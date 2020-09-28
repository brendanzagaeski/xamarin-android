# Xamarin.Android.Build.Tasks.AddKeepAlives

This library provides an MSBuild task that uses Mono.Cecil to add
`System.GC.KeepAlive()` calls at the ends of Xamarin.Android bindings library
methods, similar to <https://github.com/xamarin/java.interop/pull/722>.

Like the Java.Interop fix, the motivation for this IL-rewriting task is to
ensure that objects passed into bindings library method parameters cannot be
garbage collected too early.  This build task enables projects to test a close
approximation of the Java.Interop fix without having to rebuild all of the
bindings libraries.

_**CAUTION**_: This task rewrites IL.  Although the IL rewriting is loosely
related to what the managed linker does, the particular changes that this build
task make have so far only undergone minimal real-world testing.  The task
should accordingly be considered _**experimental**_.

Once a new version of Xamarin.Android is available that includes the
Java.Interop fix, bindings library authors are encouraged to build and publish
new versions of their libraries at their earliest convenience.  As soon as an
app can update all of its referenced bindings to versions that include the fix
from Java.Interop, this build task is no longer needed.

## Usage instructions

1. Build the NuGet package, for example by running the following command in the
   top level of the cloned repository:

   ```cmd
   msbuild -restore -t:Pack
   ```

2. Install the resulting NuGet package from
   _bin\Debug\Xamarin.Android.Build.Tasks.AddKeepAlives.1.0.0.nupkg_ into the
   target Xamarin.Android app project.

3. Build the Xamarin.Android app project in the Release configuration, or set
   the `AndroidAddKeepAlives` MSBuild property to `true`.

   For example, if building a release configuration that isn't named "Release,"
   open the _.csproj_ project file for the Xamarin.Android app project [in
   Visual Studio][edit-project-files] or another text editor, find the existing
   `<PropertyGroup>` element for the desired configuration, and add the
   following additional `<AndroidAddKeepAlives>` element within it:

   ```xml
   <AndroidAddKeepAlives>true</AndroidAddKeepAlives>
   ```

4. Clean and redeploy the project.

[edit-project-files]: https://docs.microsoft.com/visualstudio/msbuild/visual-studio-integration-msbuild#edit-project-files-in-visual-studio
