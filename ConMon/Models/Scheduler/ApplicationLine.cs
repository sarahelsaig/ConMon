using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ConMon.Models.Scheduler
{
    public class ApplicationLine
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int ApplicationId { get; set; }

        [Required]
        [DataType(DataType.Text)]
        public string Line { get; set; }


        [ForeignKey(nameof(ApplicationId))]
        public Application Application { get; set; }
    }
}
