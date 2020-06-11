using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Bogus;
using JsonApiDotNetCore.Models;
using JsonApiDotNetCore.Models.JsonApiDocuments;
using JsonApiDotNetCoreExample;
using JsonApiDotNetCoreExample.Data;
using JsonApiDotNetCoreExample.Models;
using Newtonsoft.Json;
using Xunit;
using Person = JsonApiDotNetCoreExample.Models.Person;

namespace JsonApiDotNetCoreExampleTests.Acceptance
{
    [Collection("WebHostCollection")]
    public class InjectableResourceTests
    {
        private readonly TestFixture<TestStartup> _fixture;
        private readonly AppDbContext _context;
        private readonly Faker<Person> _personFaker;
        private readonly Faker<TodoItem> _todoItemFaker;
        private readonly Faker<Passport> _passportFaker;
        private readonly Faker<Country> _countryFaker;
        private readonly Faker<Visa> _visaFaker;

        public InjectableResourceTests(TestFixture<TestStartup> fixture)
        {
            _fixture = fixture;
            _context = fixture.GetService<AppDbContext>();

            _todoItemFaker = new Faker<TodoItem>()
                .RuleFor(t => t.Description, f => f.Lorem.Sentence())
                .RuleFor(t => t.Ordinal, f => f.Random.Number())
                .RuleFor(t => t.CreatedDate, f => f.Date.Past());
            _personFaker = new Faker<Person>()
                .RuleFor(t => t.FirstName, f => f.Name.FirstName())
                .RuleFor(t => t.LastName, f => f.Name.LastName());
            _passportFaker = new Faker<Passport>()
                .CustomInstantiator(f => new Passport(_context))
                .RuleFor(t => t.SocialSecurityNumber, f => f.Random.Number(100, 10_000));
            _countryFaker = new Faker<Country>()
                .RuleFor(c => c.Name, f => f.Address.Country());
            _visaFaker = new Faker<Visa>()
                .RuleFor(v => v.ExpiresAt, f => f.Date.Future());
        }

        [Fact]
        public async Task Can_Get_Single_Passport()
        {
            // Arrange
            var passport = _passportFaker.Generate();
            passport.BirthCountry = _countryFaker.Generate();
            
            _context.Passports.Add(passport);
            _context.SaveChanges();

            var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/passports/" + passport.StringId);

            // Act
            var response = await _fixture.Client.SendAsync(request);

            // Assert
            _fixture.AssertEqualStatusCode(HttpStatusCode.OK, response);

            var body = await response.Content.ReadAsStringAsync();
            var document = JsonConvert.DeserializeObject<Document>(body);
            
            Assert.NotNull(document.SingleData);
            Assert.Equal(passport.IsLocked, document.SingleData.Attributes["isLocked"]);
        }

        [Fact]
        public async Task Can_Get_Passports()
        {
            // Arrange
            _context.Passports.RemoveRange(_context.Passports);
            _context.SaveChanges();

            var passports = _passportFaker.Generate(3);
            foreach (var passport in passports)
            {
                passport.BirthCountry = _countryFaker.Generate();
            }
            
            _context.Passports.AddRange(passports);
            _context.SaveChanges();

            var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/passports");

            // Act
            var response = await _fixture.Client.SendAsync(request);

            // Assert
            _fixture.AssertEqualStatusCode(HttpStatusCode.OK, response);

            var body = await response.Content.ReadAsStringAsync();
            var document = JsonConvert.DeserializeObject<Document>(body);
            
            Assert.Equal(3, document.ManyData.Count);
            foreach (var passport in passports)
            {
                Assert.Contains(document.ManyData,
                    resource => (long)resource.Attributes["socialSecurityNumber"] == passport.SocialSecurityNumber);
                Assert.Contains(document.ManyData,
                    resource => (string)resource.Attributes["birthCountryName"] == passport.BirthCountryName);
            }
        }

        [Fact]
        public async Task Can_Get_Passports_With_Filter()
        {
            // Arrange
            _context.Passports.RemoveRange(_context.Passports);
            _context.SaveChanges();

            var passports = _passportFaker.Generate(3);
            foreach (var passport in passports)
            {
                passport.SocialSecurityNumber = 11111;
                passport.BirthCountry = _countryFaker.Generate();
                passport.Person = _personFaker.Generate();
                passport.Person.FirstName = "Jack";
            }

            passports[2].SocialSecurityNumber = 12345;
            passports[2].Person.FirstName= "Joe";
            
            _context.Passports.AddRange(passports);
            _context.SaveChanges();

            var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/passports?include=person&filter[socialSecurityNumber]=12345&filter[person.firstName]=Joe");

            // Act
            var response = await _fixture.Client.SendAsync(request);

            // Assert
            _fixture.AssertEqualStatusCode(HttpStatusCode.OK, response);

            var body = await response.Content.ReadAsStringAsync();
            var document = JsonConvert.DeserializeObject<Document>(body);
            
            Assert.Single(document.ManyData);
            Assert.Equal(12345L, document.ManyData[0].Attributes["socialSecurityNumber"]);

            Assert.Single(document.Included);
            Assert.Equal("Joe", document.Included[0].Attributes["firstName"]);
        }

        [Fact(Skip = "https://github.com/dotnet/efcore/issues/20502")]
        public async Task Can_Get_Passports_With_Sparse_Fieldset()
        {
            // Arrange
            _context.Passports.RemoveRange(_context.Passports);
            _context.SaveChanges();

            var passports = _passportFaker.Generate(2);
            foreach (var passport in passports)
            {
                passport.BirthCountry = _countryFaker.Generate();
                passport.Person = _personFaker.Generate();
            }
            
            _context.Passports.AddRange(passports);
            _context.SaveChanges();

            var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/passports?include=person&fields=socialSecurityNumber&fields[person]=firstName");

            // Act
            var response = await _fixture.Client.SendAsync(request);

            // Assert
            _fixture.AssertEqualStatusCode(HttpStatusCode.OK, response);

            var body = await response.Content.ReadAsStringAsync();
            var document = JsonConvert.DeserializeObject<Document>(body);
            
            Assert.Equal(2, document.ManyData.Count);
            foreach (var passport in passports)
            {
                Assert.Contains(document.ManyData,
                    resource => (long)resource.Attributes["socialSecurityNumber"] == passport.SocialSecurityNumber);
            }

            Assert.DoesNotContain(document.ManyData,
                resource => resource.Attributes.ContainsKey("isLocked"));

            Assert.Equal(2, document.Included.Count);
            foreach (var person in passports.Select(p => p.Person))
            {
                Assert.Contains(document.Included,
                    resource => (string) resource.Attributes["firstName"] == person.FirstName);
            }

            Assert.DoesNotContain(document.Included,
                resource => resource.Attributes.ContainsKey("lastName"));
        }

        [Fact]
        public async Task Fail_When_Deleting_Missing_Passport()
        {
            // Arrange
            string passportId = HexadecimalObfuscationCodec.Encode(1234567890);

            var request = new HttpRequestMessage(HttpMethod.Delete, "/api/v1/passports/" + passportId);

            // Act
            var response = await _fixture.Client.SendAsync(request);

            // Assert
            _fixture.AssertEqualStatusCode(HttpStatusCode.NotFound, response);

            var body = await response.Content.ReadAsStringAsync();
            var errorDocument = JsonConvert.DeserializeObject<ErrorDocument>(body);
            Assert.Single(errorDocument.Errors);
            Assert.Equal(HttpStatusCode.NotFound, errorDocument.Errors[0].StatusCode);
            Assert.Equal("The requested resource does not exist.", errorDocument.Errors[0].Title);
            Assert.Equal("Resource of type 'passports' with id '" + passportId + "' does not exist.", errorDocument.Errors[0].Detail);
        }
    }
}
