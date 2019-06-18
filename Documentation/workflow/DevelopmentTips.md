# Development tips

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

This is for example how the `lldb` debugger launcher in Android Studio and the
Android `gdb` debugger launcher that's integrated into Visual Studio
[MIEngine][miengine] add the `lldb-server` and `gdbserver` executables to the
application's data directory.  They actually use a slightly different method to
perform the final copying step:

    $ adb shell "cat /data/local/tmp/mscorlib.dll | \
        run-as Mono.Android_Tests sh -c \
        'cat > /data/data/Mono.Android_Tests/files/.__override__/mscorlib.dll'"

This `cat` approach might be useful if you discover that the `cp` command does
not have permission to read the `/data/local/tmp/mscorlib.dll` file when you try
it on some particular device.

[miengine]: https://github.com/microsoft/MIEngine/

## Attaching `gdb` by hand

 1. Add the appropriate architecture of `gdbserver` to the application's data
    directory.  For example, if debugging an arm64-v8a app:

        $ adb push \
            ~/Library/Developer/Xamarin/android-sdk-macosx/ndk-bundle/prebuilt/android-arm64/gdbserver/gdbserver \
            /data/local/tmp/ &&
          adb shell run-as Mono.Android_Tests \
            cp /data/local/tmp/gdbserver /data/data/Mono.Android_Tests/

 2. Ensure all users have execute permissions on the application's data
    directory:

        $ adb shell run-as Mono.Android_Tests \
            chmod a+x /data/data/Mono.Android_Tests/

    This ensures that `adb` will be able to read from a UNIX domain socket
    filename in that directory.

 3. Start the app, for example by launching it with or without managed debugging
    from Visual Studio.

 4. Find the process ID of the running app, for example by using `adb shell ps`:

        $ adb shell ps | grep -F 'Mono.Android_Tests'

    Example output:

        u0_a247   15087 336   780568 69200 SyS_epoll_ 00000000 S Mono.Android_Tests

 5. Start `gdbserver`, attaching it to the running app process:

        $ adb shell run-as Mono.Android_Tests \
            /data/data/Mono.Android_Tests/gdbserver --once \
              +/data/data/Mono.Android_Tests/debug_socket \
              --attach 15087

 6. In another console window, use `adb` to forward the `debug_socket` UNIX
    domain socket to a TCP port on the local host:

        $ adb forward tcp:9999 localfilesystem:/data/data/Mono.Android_Tests/debug_socket

 7. In that new console window, run `gdb`:

        $ ~/Library/Developer/Xamarin/android-sdk-macosx/ndk-bundle/prebuilt/darwin-x86_64/bin/gdb

    And attach to the local host port:

        (gdb) target remote :9999
