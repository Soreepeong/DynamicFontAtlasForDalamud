using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Dalamud.Interface.Internal;

namespace DynamicFontAtlasLib;

public static class CustomPixelShaderMonkeyPatcher {
    internal static readonly Guid MyGuid = new("af5c9a42-714d-429c-a027-9db60c951ab6");

    public static void Patch() {
        var serviceGenericType = Assembly.GetAssembly(typeof(IDalamudTextureWrap))!.DefinedTypes
            .Single(x => x.FullName == "Dalamud.Service`1");

        var interfaceManagerType = Assembly.GetAssembly(typeof(IDalamudTextureWrap))!.DefinedTypes
            .Single(x => x.FullName == "Dalamud.Interface.Internal.InterfaceManager");

        var serviceInterfaceManagerType = serviceGenericType.MakeGenericType(interfaceManagerType);
        var interfaceManager = serviceInterfaceManagerType.GetMethod("Get")!.Invoke(null, null);
        var scene = interfaceManagerType
            .GetField("scene", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(interfaceManager)!;

        var renderer = scene.GetType()
            .GetField("imguiRenderer", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(scene)!;

        var pixelShaderField = renderer.GetType()
            .GetField("_pixelShader", BindingFlags.Instance | BindingFlags.NonPublic)!;

        var deviceObject = renderer.GetType()
            .GetField("_device", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(renderer)!;

        var pixelShaderType = deviceObject.GetType().Assembly.DefinedTypes
            .Single(x => x.FullName == "SharpDX.Direct3D11.PixelShader");

        using var stream = typeof(CustomPixelShaderMonkeyPatcher).Assembly
            .GetManifestResourceStream("imgui-frag-channel.fxc");

        var shaderData = new byte[stream!.Length];
        if (stream.Read(shaderData, 0, shaderData.Length) != shaderData.Length)
            throw new IOException();

        var oldPixelShader = (IDisposable)pixelShaderField.GetValue(renderer)!;
        var newPixelShader = Activator.CreateInstance(pixelShaderType, deviceObject, shaderData, null);
        pixelShaderField.SetValue(renderer, newPixelShader);
        oldPixelShader.Dispose();
    }
}
