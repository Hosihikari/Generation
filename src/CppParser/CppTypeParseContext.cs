namespace Hosihikari.Generation.CppParser;

public class CppTypeParseContext
{
    private readonly string? type;

    public ReadOnlySpan<char> Type
    {
        get => type;
        init
        {
            Index = value.Length - 1;
            type = new(value);
        }
    }

    public int Index { get; private set; }

    public char Next => Type[Index - 1];

    public char Previous => Type[Index + 1];

    public bool IsEnd => Index <= 0;

    public char Current => Type[Index];

    public int Length => Type.Length;

    public int this[int index] => Type[index];

    public bool MoveNext()
    {
        --Index;
        return Index >= 0;
    }

    public bool MovePrevious()
    {
        ++Index;
        return Index < Type.Length;
    }

    public void Skip(int count)
    {
        if ((count - 1) > Index)
        {
            throw new IndexOutOfRangeException();
        }

        Index -= count;
    }

    public void SkipWhitespace()
    {
        while (!IsEnd)
        {
            if (char.IsWhiteSpace(Current))
            {
                MoveNext();
                continue;
            }

            break;
        }
    }
}