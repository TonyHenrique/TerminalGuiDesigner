using System.IO;
using NUnit.Framework;
using TerminalGuiDesigner.UI;
using YamlDotNet.Serialization;

namespace tests;

public class KeyMapTests
{

    [Test]
    public void TestSerializingKeyMap()
    {
        var keys = Path.Combine(TestContext.CurrentContext.TestDirectory,"Keys.yaml");
        FileAssert.Exists(keys);

        var km = new KeyMap();

        var serializer = new Serializer();
        Assert.AreEqual(
            serializer.Serialize(km).Replace("\r\n","\n"),
            File.ReadAllText(keys).Replace("\r\n","\n"),
            "The default yaml file ('Keys.yaml') that ships with TerminalGuiDesigner should match the default values in KeyMap");

        
    }
}