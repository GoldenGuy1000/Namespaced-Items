using System.IO;

public interface IExtraData
{
    void Write(BinaryWriter writer);
    void Read(BinaryReader reader);
}