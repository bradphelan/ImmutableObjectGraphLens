using FluentAssertions;
using ImmutableObjectGraphLens;
using LanguageExt;
using static LanguageExt.Prelude;
using Xunit;

namespace ImmutableObjectGraphLensSpec
{
    public class CompanySpec
    {
        #region intermediate tests
        [Fact]
        public void ShouldBeImmutable()
        {
            var company = Company.Create();

            var lens = new
            {
                set = fun((string name, Company c) => c.With(name: name))
                ,
                get = fun((Company c) => c.Name)
            };

            company = lens.set("brad", company);
            ((object) lens.get(company)).Should().Be("brad");

        }


        [Fact]
        public void DynamicWithSingleNestingShouldWork()
        {
            var company = Company.Create();

            var lens = new
            {
                set = fun((string name, Company c) => ImmutableLens.With(c, "cto", c.Get<Person>("Cto").With("name", name)))
                ,
                get = fun((Company c) => c.Cto.Name)
            };

            company = lens.set("brad", company);
            ((object) lens.get(company)).Should().Be("brad");
            company.Cto.Name.Should().Be("brad");

        }
        [Fact]
        public void DynamicWithMultiNestingShouldWork()
        {
            var company = Company.Create();

            var lens = new
            {
                set = fun((string name, Company c) => c.WithProps(name, "cto", "name"))
                ,
                get = fun((Company c) => c.Cto.Name)
            };

            company = lens.set("brad", company);
            ((object) lens.get(company)).Should().Be("brad");
            company.Cto.Name.Should().Be("brad");

        }

        [Fact]
        public void DynamicWithUsingSelectorsShouldWork()
        {
            var company = Company.Create();

            var lens = new
            {
                set = fun((string name, Company c) => c.WithProps(name, cc => cc.Cto.Name))
                ,
                get = fun((Company c) => c.Cto.Name)
            };

            company = lens.set("brad", company);
            ((object) lens.get(company)).Should().Be("brad");
            company.Cto.Name.Should().Be("brad");

        }
        #endregion

        [Fact]
        public void LensSpec()
        {
            var company = Company.Create();

            var l =  ImmutableLens.Create((Company c)=>c.Cto.Name);

            company = l.Set(company, "brad");
            l.Get(company).Should().Be("brad");
            company.Cto.Name.Should().Be("brad");

        }
    }
}