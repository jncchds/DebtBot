namespace DebtBot.Crutch;

public class RawTextRequestAttribute : Attribute
{
    public RawTextRequestAttribute()
    {
        MediaType = "text/plain";
    }

    public string MediaType { get; set; }
}
