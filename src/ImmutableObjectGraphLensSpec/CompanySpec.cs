using System;
using FluentAssertions;
using ImmutableObjectGraphLens;
using LanguageExt;
using ReactiveUI;
using Weingartner.Lens;
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

            var l =  ImmutableLens.CreateLens((Company c)=>c.Cto.Name);

            company = l.Set(company, "brad");
            l.Get(company).Should().Be("brad");
            company.Cto.Name.Should().Be("brad");

        }

        public class Root : ReactiveObject
        {
            Company _Company = Company.Create();
            public Company Company 
            {
                get { return _Company; }
                set { this.RaiseAndSetIfChanged(ref _Company, value); }
            }
        }

        [Fact]
        public void MutableLensesShouldWork()
        {
            var root = new Root();
            var lens = new PropertyLens<Root,Company>(root,c=>c.Company);

            lens.Current.Cto.Name.Should().Be("john smith");
            var lens2 = lens.Focus(c => c.Cto.Name);
            string data = "";
            string data2 = "";

            lens2.WhenAnyValue(p => p.Current).Subscribe(current => data = current);

            lens.Observe(p=>p.Name, p => p.Cto.Name, (companyName, ctoName)=>new {companyName, ctoName})
                .Subscribe(current => data2 = current.ctoName);

            lens2.Current = "Brad";
            data.Should().Be("Brad");
            data2.Should().Be("Brad");
            lens2.Current.Should().Be("Brad");
            lens.Current.Cto.Name.Should().Be("Brad");
            root.Company.Cto.Name.Should().Be("Brad");

        }
    }
}