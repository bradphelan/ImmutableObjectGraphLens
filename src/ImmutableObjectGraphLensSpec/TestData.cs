using System.Text;
using System.Threading.Tasks;
using ImmutableObjectGraph.Generation;

namespace ImmutableObjectGraphLensSpec
{
    [GenerateImmutable]
    public partial class Company
    {
        readonly string name;
        readonly Person cto;

        static partial void CreateDefaultTemplate(ref Template template)
        {
            template.Cto = Person.Create("john smith");
            template.Name = "Microsoft";
        }

    }
    [GenerateImmutable]
    public partial class  Person
    {
        readonly string name;
    }
}
