using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using NSec.Cryptography;
using Robust.LoaderApi;

namespace SS14.Loader
{
    internal class Program
    {
        private readonly string[] _engineArgs;
        private const string RobustAssemblyName = "Robust.Client";

        private readonly IFileApi _fileApi;

        private Program(string robustPath, string[] engineArgs)
        {
            _engineArgs = engineArgs;
            var zipArchive = new ZipArchive(File.OpenRead(robustPath), ZipArchiveMode.Read);

            AssemblyLoadContext.Default.Resolving += LoadContextOnResolving;
            AssemblyLoadContext.Default.ResolvingUnmanagedDll += LoadContextOnResolvingUnmanaged;

            var prefix = "";
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                prefix = "Space Station 14.app/Contents/Resources/";
            }

            _fileApi = new ZipFileApi(zipArchive, prefix);
        }

        private IntPtr LoadContextOnResolvingUnmanaged(Assembly assembly, string unmanaged)
        {
            var ourDir = Path.GetDirectoryName(typeof(Program).Assembly.Location);
            var a = Path.Combine(ourDir!, unmanaged);
            return NativeLibrary.Load(a);
        }

        private void Run()
        {
            if (!TryOpenAssembly(RobustAssemblyName, out var clientAssembly))
            {
                Console.WriteLine("Unable to locate Robust.Client.dll in engine build!");
                return;
            }

            if (!TryGetLoader(clientAssembly, out var loader))
                return;

            var args = new MainArgs(_engineArgs, _fileApi);

            loader.Main(args);
        }

        private static bool TryGetLoader(Assembly clientAssembly, [NotNullWhen(true)] out ILoaderEntryPoint? loader)
        {
            loader = null;
            // Find ILoaderEntryPoint with the LoaderEntryPointAttribute
            var attrib = clientAssembly.GetCustomAttribute<LoaderEntryPointAttribute>();
            if (attrib == null)
            {
                Console.WriteLine("No LoaderEntryPointAttribute found on Robust.Client assembly!");
                return false;
            }

            var type = attrib.LoaderEntryPointType;
            if (!type.IsAssignableTo(typeof(ILoaderEntryPoint)))
            {
                Console.WriteLine("Loader type '{0}' does not implement ILoaderEntryPoint!", type);
                return false;
            }

            loader = (ILoaderEntryPoint) Activator.CreateInstance(type)!;
            return true;
        }

        private Assembly? LoadContextOnResolving(AssemblyLoadContext arg1, AssemblyName arg2)
        {
            return TryOpenAssembly(arg2.Name!, out var assembly) ? assembly : null;
        }

        private bool TryOpenAssembly(string name, [NotNullWhen(true)] out Assembly? assembly)
        {
            if (!TryOpenAssemblyStream(name, out var asm, out var pdb))
            {
                assembly = null;
                return false;
            }

            assembly = AssemblyLoadContext.Default.LoadFromStream(asm, pdb);
            return true;
        }

        private bool TryOpenAssemblyStream(string name, [NotNullWhen(true)] out Stream? asm, out Stream? pdb)
        {
            asm = null;
            pdb = null;

            if (!_fileApi.TryOpen($"{name}.dll", out asm))
                return false;

            _fileApi.TryOpen($"{name}.pdb", out pdb);
            return true;
        }

        internal static int Main(string[] args)
        {
            if (args.Length < 3)
            {
                Console.WriteLine("Usage: SS14.Loader <robustPath> <signature> <public key> [engineArg [engineArg...]]");
                return 1;
            }

            var robustPath = args[0];
            var sig = HexToBytes(args[1]);
            var keyPath = args[2];

            var pubKey = PublicKey.Import(
                SignatureAlgorithm.Ed25519,
                File.ReadAllBytes(keyPath),
                KeyBlobFormat.PkixPublicKeyText);

            var robustBytes = File.ReadAllBytes(robustPath);

            if (!SignatureAlgorithm.Ed25519.Verify(pubKey, robustBytes, sig))
            {
                Console.WriteLine("Failed to verify engine signature!");
                return 2;
            }

            var program = new Program(robustPath, args[3..]);
            program.Run();

            /*Console.WriteLine("lsasm dump:");
            foreach (var asmLoadContext in AssemblyLoadContext.All)
            {
                Console.WriteLine("{0}:", asmLoadContext.Name);
                foreach (var asm in asmLoadContext.Assemblies)
                {
                    Console.WriteLine("  {0}", asm.GetName().Name);
                }
            }*/

            return 0;
        }

        private static byte[] HexToBytes(ReadOnlySpan<char> hex)
        {
            var ret = new byte[hex.Length >> 1];
            for (var i = 0; i < ret.Length; i++)
            {
                var strIdx = i << 1;
                ret[i] = byte.Parse(hex[strIdx..(strIdx + 2)], NumberStyles.HexNumber);
            }

            return ret;
        }
    }
}
