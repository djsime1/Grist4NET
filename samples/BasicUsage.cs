#!/usr/bin/env dotnet
#:project ../src/Grist4NET/Grist4NET.csproj
#:property PublishAot=false

/*
 * === Grist4NET Basic Usage ===
 * - Zero boilerplate quick-start
 * - Working with unstructured dictionaries
 *
 * Demonstrated using the "Doggie Daycare" template
 * https://templates.getgrist.com/vAcfEKLQf3YF/Doggie-Daycare
 * Set your API key in the constant below
 */

using Grist4NET;

const string API_KEY = "";

if (string.IsNullOrEmpty(API_KEY))
{
	Console.ForegroundColor = ConsoleColor.Red;
	Console.WriteLine("Grist API key must be set as a code constant.");
	return 1;
}

var grist = new GristConnection(API_KEY, new Uri("https://templates.getgrist.com/api"));
var doc = await grist.GetDocument("vAcfEKLQf3YFX9GWYftQeB");

Console.WriteLine($"{doc.Name} was last modified at {doc.UpdatedAt:g}");
Console.WriteLine($"Tables: {string.Join(", ", doc.Tables.Keys)}");

var dogTable = await doc.GetTable("Dogs");
var ownerTable = await doc.GetTable("Owners");

Console.WriteLine($"There are a total of {await dogTable.CountRecords()} dogs and {await ownerTable.CountRecords()} owners.");
Console.WriteLine("Here are all owners and their dogs:");

var dogs = await dogTable.GetRecords();
var owners = await ownerTable.GetRecordsWithId();

foreach (var (id, owner) in owners)
{
	var ownedDogs = dogs.Where(dog => ((GristReference)dog["Owner"]).RowId == id).ToList();
	Console.WriteLine($"{owner["Name"]} has {ownedDogs.Count} dogs and can be reached at {owner["Phone"]} or {owner["Email"]}");
	Console.WriteLine(string.Join("\n",ownedDogs.Select(dog => $"- A {dog["Size"]} {dog["Age"]}-old {dog["Breed"]} named {dog["Puppy_Name"]}.")));
}

return 0;
