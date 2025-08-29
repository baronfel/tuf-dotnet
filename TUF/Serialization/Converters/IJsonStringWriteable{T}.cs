namespace TUF.Serialization;

interface IJsonStringWriteable<T> where T : IJsonStringWriteable<T>
{
    string ToJsonString();
}