namespace Hosihikari.Generation.CppParser;

public class CppTypeParseContext
{
    private string type;

    public ReadOnlySpan<char> Type
    {
        get => type;
        init
        {
            index = value.Length - 1;
            type = new(value);
        }
    }

    private int index;

    public int Index => index;

    public bool MoveNext()
    {
        --index;
        return index >= 0;
    }
    public bool MovePrevious()
    {
        ++index;
        return index < Type.Length;
    }

    public void Skip(int count)
    {
        if (count - 1 > index)
            throw new IndexOutOfRangeException();
        index -= count;
    }

    public char Next => Type[index - 1];

    public char Previous => Type[index + 1];

    public bool IsEnd => Index <= 0;

    public char Current => Type[index];

    public int Length => Type.Length;

    public int this[int index] => Type[index];

    public void SkipWhitespace()
    {
        while (IsEnd is false)
        {
            if (char.IsWhiteSpace(Current))
            {
                MoveNext();
                continue;
            }
            else
            {
                break;
            }
        }
    }
}