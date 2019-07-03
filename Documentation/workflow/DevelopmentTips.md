# Development tips and native debugging

Tips and tricks while developing Xamarin.Android.

## Update directory

When a Xamarin.Android app launches on an Android device, and the app was
built in the `Debug` configuration, it will create an "update" directory
during process startup, printing the created directory to `adb logcat`:

     W/monodroid( 2796): Creating public update directory: `/data/data/Mono.Android_Tests/files/.__override__`

When the app needs to resolve native libraries and assemblies, it will look
for those files within the update directory *first*. This includes the Mono
runtime library and BCL assemblies.

Note that the update directory is *per-app*. The above mentioned `Mono.Android_Tests`
directory is created when running the
[`Mono.Android-Tests.csproj`](../../src/Mono.Android/Test/Mono.Android-Tests.csproj)
unit tests.

The update directory is not used in `Release` configuration builds.
(Note: `Release` configuration for the *app itself*, not for xamarin-android.)

For example, if you're working on a mono/x86 bug and need to quickly update
the app on the device to test `libmonosgen-2.0.so` changes:

    $ make -C src/mono-runtimes/obj/Debug/x86 && \
      adb push src/mono-runtimes/obj/Debug/x86/mono/mini/.libs/libmonosgen-2.0.so \
        /data/data/Mono.Android_Tests/files/.__override__

Alternatively, if you're working on an `mscorlib.dll` bug:

    $ make -C external/mono/mcs/class/corlib PROFILE=monodroid && \
      adb push external/mono/mcs/class/lib/monodroid/mscorlib.dll \
        /data/data/Mono.Android_Tests/files/.__override__

## Update directory on modern physical devices

On modern physical devices, or modern emulators where `adbd` is running in the
default unrooted mode, `adb push` does not have write permissions for the
`/data/data/Mono.Android_Tests/files/.__override__` directory:

    adb: error: stat failed when trying to push to /data/data/Mono.Android_Tests/files/.__override__: Permission denied

To add files to the `.__override__` directory on these devices, first push them
to `/data/local/tmp/`, and then use an `adb run-as` command to copy them to the
application's data directory:

    $ adb push external/mono/mcs/class/lib/monodroid/mscorlib.dll \
        /data/local/tmp/ &&
      adb shell run-as Mono.Android_Tests cp /data/local/tmp/mscorlib.dll \
        /data/data/Mono.Android_Tests/files/.__override__/

This is also how the LLDB debugger launcher in Android Studio and the Android
GDB debugger launcher in Visual Studio [MIEngine][miengine] upload the
`lldb-server` and `gdbserver` executables.  They actually use a slightly
different command for the last step:

    $ adb shell "cat /data/local/tmp/mscorlib.dll | \
        run-as Mono.Android_Tests sh -c \
        'cat > /data/data/Mono.Android_Tests/files/.__override__/mscorlib.dll'"

This `cat` approach might be useful if you discover that the `cp` command does
not have permission to read the `/data/local/tmp/mscorlib.dll` file when you try
it on a particular device.

[miengine]: https://github.com/microsoft/MIEngine/

## Attaching LLDB using Android Studio on Windows or macOS

 1. Install [Android Studio][android-studio].  If you already have an Android
    SDK installation that you're using with Xamarin.Android, you can click
    **Cancel** on the **Android Studio Setup Wizard** when you launch Android
    Studio.

 2. Open the signed debuggable APK for the application in Android Studio via
    **Profile or debug APK** on the start window or the **File > Profile or
    Debug APK** menu item.

    ![Profile or debug in the Android Studio start
      window](../images/android-studio-start-window.png)

 3. If you skipped the **Android Studio Setup Wizard**, navigate to **File >
    Project Structure > Modules > Mono.Android_Tests-Signed > Dependencies**,
    click **New > Android SDK** next to the **Module SDK**.

    ![New SDK in the Android Studio Project Structure Modules Dependencies
      window](../images/android-studio-modules-dependencies.png)

    Select the Android SDK folder you're using with Xamarin.Android, and then
    under **Build target**, pick the appropriate Android API to match the APK,
    click **OK**, and then **OK** again.

    ![Create New Android SDK window in Android
      Studio](../images/android-studio-create-new-android-sdk.png)

 4. If an **Indexing** status message appears at the bottom of the Android
    Studio window, wait for it to complete.

 5. Start the app, for example by launching it with or without managed debugging
    from Visual Studio, or by tapping the app on the device.

 6. In Android Studio, select **Run > Attach Debugger to Android Process** (at
    the bottom of the **Run** menu), or click the corresponding toolbar item.

    ![Attach Debugger to Android Process in Android Studio Run
      menu](../images/android-studio-attach-debugger.png)

 7. Set the **Debugger** to **Native**, select the running app, and click
    **OK**.

    If the `adb` connection is slow, the first connection to the app will take a
    while to download all the system libraries.  The connection might time out
    if this takes too long, but the next connection attempt will have fewer
    libraries left to download and will likely succeed.

 8. Depending on the scenario you are debugging, LLDB might break on the signals
    that Mono uses internally.  If it does, you can set LLDB to continue through
    those by opening **View > Tool Windows > Debug**, selecting the **Android
    Native Debugger** tab, and then navigating to the inner **Debugger \[tab\] >
    LLDB \[tab\]** command prompt, and running the following `process handle`
    command:

        (lldb) process handle -p true -n true -s false SIGXCPU SIG33 SIG35 SIGPWR SIGTTIN SIGTTOU SIGSYS

    ![LLDB process handle command in Android Studio LLDB command
      prompt](../images/android-studio-lldb-no-stop-signals.png)

[android-studio]: https://developer.android.com/studio/

## Adding debug symbols for a published `libmonosgen-2.0` version

### Option A: Add the `libmonosgen-2.0.d.so` with symbols as an `@(AndroidNativeLibrary)`

This option only works if **Android Options > Use Shared Runtime** is enabled in
the Visual Studio project property pages.

 1. Find the Xamarin.Android version you are debugging on
    <https://github.com/xamarin/xamarin-android/tags>, and click that version to
    view the release information.

 2. Click the link for the corresponding open-source build under the **OSS
    core** section of the release information.

 3. Navigate to the **Azure Artifacts** from the left sidebar, and download the
    `xamarin-android/xamarin-android/bin/Release/bundle*.zip` file.

 4. Extract the `libmonosgen-2.0.d.so` files from the bundle.  For example, run:

        $ unzip bundle*.zip '**libmonosgen-2.0.d.so'

    (On Windows, the Git Bash command prompt includes the `unzip` command, so
    that's one easy way to complete this step.)

 5. Add the appropriate architecture of `libmonosgen-2.0.d.so` to
    the corresponding `lib` subdirectory of the project as described in the
    [Using Native Libraries][using-native-libraries] documentation.  Then rename
    the file to `libmonosgen-2.0.so`.  For example, if debugging an arm64-v8a
    app, add the arm64-v8a version of `libmonosgen-2.0.d.so` to the project as
    `lib/arm64-v8a/libmonosgen-2.0.so`.

    ![libmonosgen-2.0.so added to the lib/arm64-v8a directory of the
      Xamarin.Android app project in the Visual Studio Solution
      Explorer](../images/lib-arm64-v8a-libmonosgen.png)

 6. Set the **Build Action** of the file to **AndroidNativeLibrary**.

    ![Build Action for libmonosgen-2.0.so set to AndroidNativeLibrary in the
      Visual Studio Properties
      window](../images/build-action-android-native-library.png)

 7. Build, deploy, and run the app, and then attach LLDB.

 8. If desired, follow the `image lookup` and `settings set --
    target.source-map` steps from the [Debugging Mono binaries with LLDB
    guide][lldb-source-map] to allow stepping through the Mono runtime souce
    files.

[using-native-libraries]: https://docs.microsoft.com/xamarin/android/platform/native-libraries
[lldb-source-map]: https://www.mono-project.com/docs/debug+profile/debug/lldb-source-map/

### Option B: Upload `libmonosgen-2.0.d.so` with symbols to the `.__override__` update directory

 1. Download and extract the `libmonosgen-2.0.d.so` files as described for
    Option A.

 2. Push the appropriate architecture of `libmonosgen-2.0.d.so` into the
    application's update directory, renaming it to `libmonosgen-2.0.so` along
    the way:

        $ adb push libmonosgen-2.0.d.so \
            /data/local/tmp/libmonosgen-2.0.so &&
          adb shell run-as Mono.Android_Tests cp /data/local/tmp/libmonosgen-2.0.so \
            /data/data/Mono.Android_Tests/files/.__override__/

 3. Ensure all users have execute permissions on the application's data
    directory:

        $ adb shell run-as Mono.Android_Tests \
            chmod a+x /data/data/Mono.Android_Tests/

    This ensures that LLDB will be able to download `libmonosgen-2.0.so` from
    the update directory to read the symbols for it.

 4. Run the app and attach LLDB.

### Option C: Manually load the `libmonosgen-2.0.d.so` with symbols into LLDB

 1. Download and extract the `libmonosgen-2.0.d.so` files as described for
    Option A.

 2. After attaching to the app with LLDB, add the appropriate architecture of
    `libmonosgen-2.0.d.so` into LLDB, using a command similar to:

        (lldb) image add ~/Downloads/lib/xamarin.android/xbuild/Xamarin/Android/lib/arm64-v8a/libmonosgen-2.0.d.so

 3. Find the current in-memory address of the `.text` section of the shared
    runtime version of `libmonosgen-2.0`.  For example, for a 64-bit app, run
    the following command:

        (lldb) image dump sections libmonosgen-64bit-2.0.so

    Look for the row of the table that shows "code" as the "Type":

        SectID     Type             Load Address                             Perm File Off.  File Size  Flags      Section Name
        ---------- ---------------- ---------------------------------------  ---- ---------- ---------- ---------- ----------------------------
        0x0000000a code             [0x00000071106c4e80-0x0000007110932674)  r-x  0x0002ee80 0x0026d7f4 0x00000006 libmonosgen-64bit-2.0.so..text

 4. Now load the full `libmonosgen-2.0.d.so` using the same starting address for
    the `.text` section:

        (lldb) image load -f libmonosgen-2.0.d.so .text 0x00000071106c4e80

## Using a custom `libmonosgen-2.0.so`

Follow the steps from *Add the `libmonosgen-2.0.d.so` with symbols as an
`@(AndroidNativeLibrary)`* or *Upload `libmonosgen-2.0.d.so` with symbols to the
`.__override__` update directory*, but use a custom locally built version of
`libmonosgen-2.0.so` instead of a prebuilt `libmonosgen-2.0.d.so`.

## Other options for attaching LLDB or GDB

### Attaching LLDB using mono/lldb-binaries on macOS

Download the precompiled `lldb` and `lldb-server` binaries from
<https://github.com/mono/lldb-binaries/releases>, and follow the instructions
within [README.md][lldb-readme].

[lldb-readme]: https://github.com/mono/lldb-binaries/blob/master/README.md

### Attaching GDB using Visual Studio on Windows

 1. In the Visual Studio Installer, under the **Individual components** tab,
    ensure that **Development activities > C++ Android development tools** is
    installed.

 2. Install the Android NDK if you don't already have it installed.  For
    example, use **Tools > Android > Android SDK Manager** in Visual Studio to
    install it.

 3. Set **Tools > Options > Cross Platform > C++ > Android > Android NDK** to
    the Android NDK path.  For example, if you installed the Android NDK using the
    Android SDK Manager in Visual Studio, set the path to:

        C:\Microsoft\AndroidNDK64\android-ndk-r15c

 4. Quit and relaunch Visual Studio to load the Android NDK path.

 5. Use **File > Open > Project/Solution** to open the signed debuggable APK for
    the application.

 6. Set the **Build > Configuration Manager > Active solution platform** to the
    application ABI.  If debugging an arm64-v8a application, explicitly add the
    `ARM64` platform to the solution and set it as the active platform.

 7. If you need symbols for `libmonosgen-2.0`, copy the library file with
    symbols to a convenient location, and make sure the file name of the library
    matches the name on device (for example, `libmonosgen-64bit-2.0.so` if using
    the shared runtime).  Then add the local directory containing that library
    to **Project > Properties > Additional Symbol Search Paths** in the native
    debugging project.

 8. Start the app, for example by launching it with or without managed debugging
    from Visual Studio, by tapping the app on the device.

 9. Select **Debug > Attach to Android process** and wait for the connection to
    complete.

10. If needed, you can use **Debug > Windows > Immediate** to interact with the
    GDB command line.  Prefix normal GDB commands with `-exec` in the
    interactive window to get the expected behavior.  For example to view the
    stack backtrace:

        -exec backtrace

11. Depending on the scenario you are debugging, GDB might break on the signals
    that Mono uses internally.  If it does, you can set GDB to continue through
    those by opening the Immediate window and running the following `handle`
    command:

        -exec handle SIGXCPU SIG33 SIG35 SIGPWR SIGTTIN SIGTTOU SIGSYS nostop noprint
