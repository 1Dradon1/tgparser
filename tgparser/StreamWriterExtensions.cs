namespace TdLib.Samples.GetChats;

internal static class StreamWriterExtensions
{
    public static void SerializeAndWrite(this StreamWriter writer, object content)
    {
        var serializedYamlData = Program.Serializer.Serialize((dynamic)content);
        writer.WriteLine(serializedYamlData);
        writer.Flush();
    }
}
