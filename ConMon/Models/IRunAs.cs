namespace ConMon.Models
{
    public interface IRunAs
    {
        string Domain { get; set; }
        string Pass { get; set; }
        string User { get; set; }
    }
}