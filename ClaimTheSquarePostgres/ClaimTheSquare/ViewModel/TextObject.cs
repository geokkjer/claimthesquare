namespace ClaimTheSquare.ViewModel;

public class TextObject
{
    public int Index { get; set; }
    public string Text { get; set; } = "";
    public string BackColor { get; set; } = "";
    public string ForeColor { get; set; } = "";

    public TextObject(int index, string text, string backColor, string foreColor)
    {
        Index = index;
        Text = text;
        BackColor = backColor;
        ForeColor = foreColor;
    }

    // Den tomme konstruktøren er ikke unødvendig: System.Text.Json bruker den
    // når den deserialiserer innkommende JSON, og Dapper bruker den når den
    // mater rader inn i objekter. Legger du til en konstruktør med parametere,
    // forsvinner den implisitte tomme — derfor står den her eksplisitt.
    public TextObject()
    {
    }
}
