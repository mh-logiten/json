namespace Logiten.Json
{
    public enum JsonToken
    {
        Unknown,
        ObjectStart,
        PropertyName,
        NumberValue,
        NullValue,
        StringStart,
        StringEnd,
        ObjectEnd,        
    }
}