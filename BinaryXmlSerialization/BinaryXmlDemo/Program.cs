using BinaryXmlSerialization;
using System.Diagnostics;
using System.Runtime.Serialization.Formatters.Binary;

var person = Person.CreateRandomPerson();

Person deserializedPerson = null;

// One off serialization of object to binary xml file
using (var stream = new FileStream("person.bxml", FileMode.Create))
{
    await BinaryXmlSerializer<Person>.SerializeAsync(person, stream);
}

// One off deserialization of object from binary xml file
using (var stream = new FileStream("person.bxml", FileMode.Open))
{
    // The Binary XML session is included in the stream so passing null for the session data
    deserializedPerson = BinaryXmlSerializer<Person>.Deserialize(stream, null);
}

Console.WriteLine("Original person:");
Console.WriteLine(person);
Console.WriteLine("Deserialized person:");
Console.WriteLine(deserializedPerson);
Console.WriteLine();

#pragma warning disable SYSLIB0011 // Type or member is obsolete
BinaryFormatter binaryFormatter = new BinaryFormatter();
#pragma warning restore SYSLIB0011 // Type or member is obsolete

using (var stream = new FileStream("person.dat", FileMode.Create))
{
    binaryFormatter.Serialize(stream, person);
}

Console.WriteLine($"Size of single person using binary xml: {new FileInfo("person.bxml").Length}");
Console.WriteLine($"Size of single person using binary formatter: {new FileInfo("person.dat").Length}");
Console.WriteLine();

// If you maintain state and save the session data separately, you can benefit from some major space saving. Be careful that the session state has everything that's needed
// and that you don't serialize new objects later that depend on new strings stored in the session state which aren't saved.

// Create a reusable serialization wrapper which holds the state
var personBinaryXmlSerializer = new BinaryXmlSerializer<Person>();
long totalBinaryXmlBytes = 0;
long totalBinaryFormatterBytes = 0;
var ms = new MemoryStream();
for (int i = 0; i < 10; i++)
{
    var personToSerialize = Person.CreateRandomPerson();
    ms.Position = 0;
    ms.SetLength(0);
    binaryFormatter.Serialize(ms, personToSerialize);
    totalBinaryFormatterBytes += ms.Length;
    ms.Position = 0;
    ms.SetLength(0);
    await personBinaryXmlSerializer.SerializeAsync(personToSerialize, ms, false);
    totalBinaryXmlBytes += ms.Length;
}

// Last person was serialized to ms to maintain length to show deserializing with seprate session state
ms.Position = 0;
MemoryStream sessionDataStream = new MemoryStream();
await personBinaryXmlSerializer.WriteSessionDataAsync(sessionDataStream);
sessionDataStream.Position = 0;
totalBinaryXmlBytes += sessionDataStream.Length;

Console.WriteLine($"Size of 10 random people using binary xml with shared session data: {totalBinaryXmlBytes}");
Console.WriteLine($"Size of shared session state (included in the previous number): {sessionDataStream.Length}");
Console.WriteLine($"Size of 10 random people using binary formatter: {totalBinaryFormatterBytes}");
Console.WriteLine();

// ms Contains the last person serialized with binary xml and sessionDataStream contains the session data
// Create a new instance as though we are deserializing in a different process
personBinaryXmlSerializer = new BinaryXmlSerializer<Person>();
// Read the shared session data into the instance
personBinaryXmlSerializer.ReadSharedSessionData(sessionDataStream);
deserializedPerson = personBinaryXmlSerializer.Deserialize(ms);
Console.WriteLine("Deserialized person read using separate session data:");
Console.WriteLine(deserializedPerson);
Console.WriteLine();

// Serialize a list of 1000 Person objects and compare sizes
var listOfPerson = new List<Person>(1000);
for (int i = 0; i < 1000; i++)
{
    listOfPerson.Add(Person.CreateRandomPerson());
}
ms.Position = 0;
ms.SetLength(0);
await BinaryXmlSerializer<List<Person>>.SerializeAsync(listOfPerson, ms);
Console.WriteLine($"Size of List of 1000 random people using binary xml: {ms.Length}");
ms.Position = 0;
ms.SetLength(0);
binaryFormatter.Serialize(ms, listOfPerson);
Console.WriteLine($"Size of List of 1000 random people using binary formatter: {ms.Length}");
Console.WriteLine();

// Time to measure how long it takes to serialize data
Stopwatch sw = Stopwatch.StartNew();
for (int i = 0; i < 10000; i++)
{
    ms.Position = 0;
    ms.SetLength(0);
    await BinaryXmlSerializer<List<Person>>.SerializeAsync(listOfPerson, ms);
}
TimeSpan binaryXmlSerializationTime = sw.Elapsed;
sw.Restart();
for (int i = 0; i < 10000; i++)
{
    ms.Position = 0;
    var output = BinaryXmlSerializer<List<Person>>.Deserialize(ms, null);
}
TimeSpan binaryXmlDeserializationTime = sw.Elapsed;
sw.Restart();
for (int i = 0; i < 10000; i++)
{
    ms.Position = 0;
    ms.SetLength(0);
    binaryFormatter.Serialize(ms, listOfPerson);
}
TimeSpan binaryFormatterSerializationTime = sw.Elapsed;
sw.Restart();
for (int i = 0; i < 10000; i++)
{
    ms.Position = 0;
    var output = binaryFormatter.Deserialize(ms);
}
TimeSpan binaryFormatterDeserializationTime = sw.Elapsed;

Console.WriteLine($"Binary XML serialization total time: {binaryXmlSerializationTime.Seconds}s {binaryXmlSerializationTime.Milliseconds:D3}.{binaryXmlSerializationTime.Microseconds:D3}ms, {(binaryXmlSerializationTime / 10000).TotalMilliseconds}ms per iteration");
Console.WriteLine($"Binary XML deserialization total time: {binaryXmlDeserializationTime.Seconds}s {binaryXmlDeserializationTime.Milliseconds:D3}.{binaryXmlDeserializationTime.Microseconds:D3}ms, {(binaryXmlDeserializationTime / 10000).TotalMilliseconds}ms per iteration");
Console.WriteLine($"Binary formatter serialization total time: {binaryFormatterSerializationTime.Seconds}s {binaryFormatterSerializationTime.Milliseconds:D3}.{binaryFormatterSerializationTime.Microseconds:D3}ms, {(binaryFormatterSerializationTime / 10000).TotalMilliseconds}ms per iteration");
Console.WriteLine($"Binary formatter deserialization total time: {binaryFormatterDeserializationTime.Seconds}s {binaryFormatterDeserializationTime.Milliseconds:D3}.{binaryFormatterDeserializationTime.Microseconds:D3}ms, {(binaryFormatterDeserializationTime / 10000).TotalMilliseconds}ms per iteration");
