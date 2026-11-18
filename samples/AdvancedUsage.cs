#!/usr/bin/env dotnet
#:project ../src/Grist4NET/Grist4NET.csproj
#:package Microsoft.Extensions.Hosting@10.0.12
#:package Microsoft.Extensions.Configuration.Binder@10.0.12
#:property PublishAot=false

/*
 * === Grist4NET Advanced Usage ===
 * - Dependency injection
 * - Options pattern
 * - Filtering records
 * - Binding records to POCO
 *
 * Demonstrated using the "Doggie Daycare" template
 * https://templates.getgrist.com/vAcfEKLQf3YF/Doggie-Daycare
 * Set your API key in appsettings.json
 */

using Grist4NET;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.Configure<GristConnectionOptions>(builder.Configuration.GetSection("Grist"));
builder.Services.AddSingleton<GristConnection>();

var app = builder.Build();
var config = app.Services.GetRequiredService<IConfiguration>();

if (string.IsNullOrEmpty(config.GetSection("Grist")["ApiKey"]))
{
	Console.ForegroundColor = ConsoleColor.Red;
	Console.WriteLine("Grist API key must be set in appsettings.json");
	return 1;
}

var grist = app.Services.GetRequiredService<GristConnection>();
var docid = config.GetSection("Grist")["DocumentId"];
var doc = await grist.GetDocument(docid);

Console.WriteLine($"{doc.Name} was last modified at {doc.UpdatedAt:g}");
Console.WriteLine($"Tables: {string.Join(", ", doc.Tables.Keys)}");

var dogTable = await doc.GetTable("Dogs");
var ownerTable = await doc.GetTable("Owners");

Console.WriteLine($"There are a total of {await dogTable.CountRecords()} dogs and {await ownerTable.CountRecords()} owners.");
Console.WriteLine("Here are owners and their dogs from Brooklyn:");

var dogs = await dogTable.BindRecords<Dog>(new GristCellValueFilter<string>("Location") {"Brooklyn"});
var owners = await ownerTable.BindRecords<Owner>();

foreach (var (id, owner) in owners)
{
	var ownedDogs = dogs.Values.Where(dog => dog.Owner.RowId == id).ToList();
	if (ownedDogs.Count == 0)
		continue;
	Console.WriteLine($"{owner.Name} has {ownedDogs.Count} dogs and can be reached at {owner.Phone} or {owner.Email}");
	Console.WriteLine(string.Join("\n",ownedDogs.Select(dog => $"- A {dog.Size} {dog.Age}-old {dog.Breed} named {dog.Name}.")));
}

return 0;

public class Dog
{
	public string Location { get; set; }
	[GristColumnId("Puppy_Name")]
	public string Name { get; set; }
	public string Age { get; set; }
	public string Breed { get; set; }
	public DogSize Size { get; set; }
	public GristReference Owner { get; set; }
	public string Plan { get; set; }
	public DogPromotion? Promotion { get; set; }
	[GristColumnId("DHLPP_")]
	public DateOnly DHLPP { get; set; }
	[GristColumnId("Rabies_")]
	public DateOnly Rabies { get; set; }
	public DateOnly? Bordetella { get; set; }
	public DateOnly? Lyme { get; set; }
	[GristColumnId("Canine_Influenza")]
	public DateOnly? CanineInfluenza { get; set; }
	[GristColumnId("Corona_Virus")]
	public DateOnly? CoronaVirus { get; set; }
}

public class Owner
{
	public string Name { get; set; }
	public string Phone { get; set; }
	public string Email { get; set; }
}

public enum DogSize
{
	Small,
	Medium,
	Large
}

public enum DogPromotion
{
	[GristChoice("Grand Opening")]
	GrandOpening,
	[GristChoice("2 dogs for 1")]
	TwoForOne
}
