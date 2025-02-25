# Binary XML Serialization

## The Problem
Now that `BinaryFormatter` has been deprecated, many are looking for an alternative solution. Switching serializers can be a lot of work as different serializers work in different ways. A class which is serializable (and importantly deserializable) with one serializer will often not correctly serialize with a different serializer. This is due to the decisions made as to what to serialize, and how to know what not to serialize. For example, `BinaryFormatter` requires a type to have the `SerializableAttribute` attribute applied. When a class has this applied, all the fields are serialized, unless a field is attributed with the `NonSerializedAttribute` attribute. Then you have support for the attributes `OptionalFieldAttribute`, `OnSerializingAttribute`, `OnSerializedAttribute`, `OnDeserializingAttribute`, and `OnDeserializedAttribute`. Instead of using these attributes, a class might implement the `ISerializable` interface.

If a type has a lot of careful usage of these features, a serializer which doesn't support the features being used is likely to do the wrong thing.

## The Solution
The `DataContractSerializer` serializer supports all of these features. While it's important to validate that any custom behaviors a type has already implemented are being used and having the desired effect, in the majority of cases, `DataContractSerializer` should round trip data to and from a serialized format successfully whenever `BinaryFormatter` was able to do so too.

One exception to this is when the object stored in a class member being serialized is a derived type of what was declared for the member. For security reasons, `DataContractSerializer` will only serialize the exact types that are declared in the class. For example, if you have a field declared as type `Dog`, and you store in that field an object of type `Labrador` (which is derived from `Dog`), `DataContractSerializer` won't be able to serialize the stored object as it will only know how to serialize the declared type `Dog`.

The simple fix for this is to either use the `KnownTypeAttribute` to specify the other types that are expected, or to pass the set of known types to the constructor of `DataContractSerializer` (either directly or via `DataContractSerializerSettings`).

## Performance
If you serialize with DCS by using the `WriteObject` overload that takes a `Stream`, the resulting serialized payload will be a text-based XML document written using UTF-8. This is useful when first migrating to DCS to validate that all the data you expect to be in the output is there, but it isn't the most efficient way to use DCS.

### Binary XML
There are multiple `XmlWriter` implementations that ship with .NET. They have various behaviors and optimizations. For example, there's one which will validate that the XML you are writing is well-formed. There is a special type of `XmlWriter` (and corresponding `XmlReader`) which are intended for more efficient reading and writing of XML. These are `XmlDictionaryWriter` and `XmlDictionaryReader`. There are binary variants of these which write a binary representation of the XML. This has some nice tricks like writing variable length integers, or writing `DateTime` instances as a 64-bit unsigned integer (which is a lot smaller than the textual representation in XML).

When writing an XML element, it has a name. E.g. `<dog>` has the name "dog". This needs to be preserved so that when you read back the serialized payload, DCS can know what name was originally written to match it up with the appropriate class member when deserializing. Strings are normally written to the binary XML verbatim, unless you establish an XML binary session. With an XML binary session, any new dictionary strings (these are strings used in the XML structure) that haven't been seen before are allocated a unique integer value (just an integer that increments by 1 for each new string) and the integer value is used in the binary XML output. The mapping of integer value to string needs to be stored/transmitted with the payload so that when reading the binary XML, the dictionary string integer values can be looked up.

To track new strings which are added to the session, an implementation of `XmlBinaryWriterSession` is passed to `XmlDictionaryWriter.CreateBinaryWriter`. When dictionary strings are written to an `XmlDictionaryWriter` and you are using a session, they are first looked up in the session, and if the string exists, the assigned id number is used. If the string doesn't exist in the session, then it's added and a new id assigned. When reading binary XML, the session data needs to be loaded into an instance of `XmlBinaryReaderSession`, which is then passed to `XmlDictionaryReader.CreateBinaryReader`.

There isn't an implementation of `XmlBinaryWriterSession` included in .NET that enables you to extract the set of strings and their ids after serialization is complete. An implementation will need to be derived from `XmlBinaryWriterSession` which separately keeps track of any added strings. After serialization has completed, the session is checked for any new strings, and then needs to be written somewhere to be consumed prior to deserializing the payload. The `XmlBinaryReaderSession` class is usable without needing to derive a new type when deserializing.

#### To share or not to share, that is the question
Sometimes it is appropriate to use a single `XmlBinaryWriterSession` for multiple serialized payloads and to store the final session dictionary alongside the serialized payloads. This allows you to store/transmit a single session data for multiple serialized objects for efficiency purposes. For example, if you have 100 objects you wish to serialize independently, but exist as part of a batch, you can instantiate a single `XmlBinaryWriterSession` instance and reuse it without resetting the state while serializing 100 different payload outputs. When you are finished, you would write out the session data just once. When it's time to deserialize, you would read the session data into a shared `XmlBinaryReaderSession`, then reuse it for deserializing all the 100 serialized payloads. This way you only have the cost overhead of the session data once.

If you don't wish to share the session data for multiple serialization payloads, a useful convention is to store the session data at the beginning of the stored/transmitted payload. At deserialization time, the session data is read, stored into an `XmlBinaryReaderSession`, which is then used to create a binary XML reader, and then deserialize the rest of the payload.

A hybrid approach is also possible if you have a known order to a series of payloads. You serialize the first object, and prepend the session data so far. For the second object, you reuse the same `XmlBinaryWriterSession` instance with the state such that it knows about the session strings that have been emitted up to this point, and tracks any new session strings that are added. After serializing the second object, write out only new session data before the second serialized payload. Keep repeating this for each subsequent serialization. This is useful for an ordered series of data, and is what WCF/CoreWCF does when using a persistent session with binary message encoding. The constraint with this method is all data must be deserialized in the same order that it was serialized.

### Implementation
I have provided an implementation of `XmlBinaryWriterSession` in the class [TrackingXmlBinaryWriterSession](./BinaryXmlSerialization/TrackingXmlBinaryWriterSession.cs). As new strings are added to the session, it stores them in a separate array. Once serialization is complete, you check if there's any new strings and write them out. I have created a sample implementation of how to put all these together. I've created the generic class [BinaryXmlSerializer\<T\>](./BinaryXmlSerialization/BinaryXmlSerializer.cs) which has static and instance methods to help serialize/deserialize an object to/from binary XML. The static methods can be used when using the model where the complete session data is prepended to the serialization payload every time. You can also create an instance of the class to save shared session data and write it out separately. You can then load shared session data at a later time and use it when deserializing serialized payloads.

### Size comparison with BinaryFormatter
I've included a demo of serializing an example `Person` class using `DataContractSerializer` via the provided `BinaryXmlSerializer<T>` helper class. You can find the code for this demo [here](./BinaryXmlDemo/Program.cs). The `Person` class has a static method for creating randomized instances with different variations on the values. It has multiple backing fields as we're serializing in the same way as BinaryFormatter so serializing fields using the `SerializableAttribute`. Due to the random nature of the generated instances, my results will be slightly different than running it yourself, but they will be similar. When running the demo, this is the output expressing the size difference between serializing the same instances using `BinaryFormatter` and `DataContractSerializer`:
```
Size of single person using binary xml: 295
Size of single person using binary formatter: 365
```
DCS produced a serialized payload that is roughly 24% smaller than `BinaryFormatter` produced. The next demo is using a shared session data. 10 random instances are created, serialized with `BinaryFormatter` and DCS, and the total payload size of each serialization summed. Then the DCS shared session data is written out and its size added to the DCS total. Here is the output from that section of the demo:
```
Size of 10 random people using binary xml with shared session data: 1186
Size of shared session state (included in the previous number): 191
Size of 10 random people using binary formatter: 3605
```
In this scenario, binary formatter produces an output more than 3 times the size of the DCS output. The difference isn't always so large. When dealing with lists/arrays, XML is less efficient than binary formatter. When writing out a list of values to XML, even in binary form, there is a start and end element written around each item of the list. Binary formatter doesn't need this extra structural data so the gains aren't as large.
```
Size of List of 1000 random people using binary xml: 95014
Size of List of 1000 random people using binary formatter: 102200
```
The binary xml payload is still smaller than binary formatter, but the difference is a lot less.

### CPU time comparison with BinaryFormatter
The demo includes some very simple performance measurements. I take the list of 1000 instances and in a loop serialize it 10,000 times. A Stopwatch is used to record how much time this takes. The payload is then deserialized 10,000 times, and is likewise timed. The total time as well as per serialization/deserialization time is then reported. The size of the payload will vary from run to run due to generating random `Person` instances, and the timing values will vary depending on the specific performance of the machine running the demo, but the results should be comparable at least at a qualitative level.
```cmd
Binary XML serialization total time: 6s 248.543ms, 0.6249ms per iteration
Binary XML deserialization total time: 13s 716.416ms, 1.3716ms per iteration
Binary formatter serialization total time: 19s 620.453ms, 1.962ms per iteration
Binary formatter deserialization total time: 19s 494.786ms, 1.9495ms per iteration
```
The binary XML serialization was more than 3 times faster, and the deserialization was about 50% faster. Overall you can see that using DCS will produce comparable if not smaller payloads, and will be faster. It's feature comparable and in many cases a drop in replacement for `BinaryFormatter`.
