# Performance Types

[![NuGet version](https://badge.fury.io/nu/PerformanceTypes.svg)](http://badge.fury.io/nu/PerformanceTypes)
[![Build status](https://ci.appveyor.com/api/projects/status/ielvdv17v6qh2py3?svg=true)](https://ci.appveyor.com/project/bretcope/performancetypes)

This library is a small collection of specialized helper types which primarily focus on reducing allocations compared to BCL alternatives. __This is NOT a general-purpose library__. If you are not concerned with managed heap allocations, you will likely be better off sticking to BCL-provided types and methods.

- [StopwatchStruct](#stopwatchstruct): A value-type implementation of Stopwatch for benchmarking without allocations.
- [ReusableStream](#reusablestream): A [Stream](https://msdn.microsoft.com/en-us/library/system.io.stream(v=vs.110).aspx) implementation which reads from a byte array, and:
  - Allows the underlying data array to be swapped out without allocating a new wrapper stream.
  - Integrates with [StringSet](#stringset) to reduce allocations when reading strings from a byte stream.
  - Provides helper methods similar to [BinaryReader](https://msdn.microsoft.com/en-us/library/system.io.binaryreader(v=vs.110).aspx) and [BinaryWriter](https://msdn.microsoft.com/en-us/library/system.io.binarywriter(v=vs.110).aspx).
  - Provides several unsafe methods for reading and writing from byte pointers.
- [StringSet](#stringset): A specialized hash set for strings which allows lookups by hash code.
  - [StringHash](#stringhash)
- [UnsafeStringComparer](#unsafestringcomparer): A set of string-comparison methods where one or more operands are a char array or pointer.
- [UnsafeMd5](#unsafemd5): An [MD5](https://en.wikipedia.org/wiki/MD5) hashing implementation which performs zero allocations.
- Unsafe: a small collection of unsafe static utility methods.
  - [MemoryCopy](#memorycopy): A quick alternative to `memcpy` for small data buffers (~400 bytes or less).
  - [ToHexString](#tohexstring): Returns the hexadecimal representation of an unmanaged buffer.

## StopwatchStruct

[StopwatchStruct](https://github.com/bretcope/PerformanceTypes/blob/master/PerformanceTypes/StopwatchStruct.cs) is a partial re-implementation of the [Stopwatch](https://msdn.microsoft.com/en-us/library/system.diagnostics.stopwatch) class with methods for `Start()`, `Stop()`, and `GetElapsedMilliseconds()` plus an `Elapsed` property.

> This struct will only work on Windows because it relies on calls to QueryPerformanceCounter for high-resolution time measurements.

The main advantage over the Stopwatch class is that StopwatchStruct is a value type, and therefore can be allocated on the stack, where it won't incur any garbage collection 

```csharp
var sw = new StopwatchStruct();
sw.Start();
DoSomething();
sw.Stop();

double ms = sw.GetElapsedMilliseconds();
// or
TimeSpan elapsed = sw.Elapsed;
```

>  StopwatchStruct is intended to be started and stopped within a single method. It's generally not recommended to pass the struct to other methods or return it unless you understand all the implications of pass-by-value for a mutable struct.

## ReusableStream

The primary use case for ReusableStream is when you want to read or write binary data to a byte array. A [MemoryStream](https://msdn.microsoft.com/en-us/library/system.io.memorystream(v=vs.110).aspx) wrapped in a [BinaryReader](https://msdn.microsoft.com/en-us/library/system.io.binaryreader(v=vs.110).aspx) or [BinaryWriter](https://msdn.microsoft.com/en-us/library/system.io.binarywriter(v=vs.110).aspx) works well for this most use cases. However, those options don't allow you to swap out the underlying data source. If you want to read or write to a new buffer, you need to allocate new wrappers. They also make it difficult to reset the stream or read or write from pointers.

> Most methods on this class rely on the native endianness of the system. A stream constructed on a big-endian system will not be compatible with one constructed on a little-endian system.

### Construction

```csharp
// auto-create the underlying data array
var stream = new ReusableStream(initialCapacity);

// use a pre-created underlying data array
var stream = new ReusableStream(buffer, startIndex, readableLength);

// create the stream without underlying data - MUST call ReplaceData before using stream
var stream = new ReusableStream();
stream.ReplaceData(buffer, startIndex, readableLength);
```

### Read/Write

```csharp
// helper methods exist for reading and writing primitives
stream.Write(17);
stream.ReadInt32();

// reading into a managed buffer
byte[] bytes = new byte[16];
int bytesRead = stream.Read(bytes, 0, bytes.Length);

// reading into an unmanaged buffer
byte* bytes = stackalloc byte[16];
int bytesRead = stream.Read(bytes, 16);

// writing from a buffer
stream.Write(buffer, 0, buffer.Length);
stream.Write(bufferPtr, bufferLength);
```

### Resetting the Stream

There are three forms of resetting.

1. __Replace the underlying data__ (by calling `ReplaceData()`): resets everything about the stream including length and position.
2. __Reset for reading__ (`ResetForReading()`): resets the position of the stream back to the initial offset, but does not adjust the length. Useful if you want to re-read all data in the stream. Equivalent to calling `Seek(0, SeekOrigin.Begin)`.
3. __Reset for writing__ (`ResetForWriting()`). Resets the position and length of the stream. Useful if you don't care about the underlying data anymore and want to write over it.

### Strings

Strings can be written or read using any encoding. The default encoding is UTF8. You must specify (via the second parameter) whether the string should be considered nullable or not.

```csharp
stream.DefaultEncoding = Encoding.UTF32; // change the default encoding

var nullableString = stream.ReadString(true); // read nullable string
var nonNullableString = stream.ReadString(false); // read non-nullable string

stream.WriteString(nullableString, true);
stream.WriteString(nonNullableString, false);

// read/write a single string using a different encoding
var str = stream.ReadString(true, Encoding.Unicode);
stream.WriteString(str, true, Encoding.Unicode);
```

#### Interning

When deserializing data, it is common to read the same strings over and over. In some cases, it may be preferable to reuse pre-allocated string objects rather than allocating a new string every time. For this, ReusableStream supports integrating with [StringSet](#stringset).

```csharp
var set = new StringSet(initialCapacity);

// add the strings you expect to see
var testString = "test";
set.Add(testString);
set.Add("cat");
set.Add("dog");
...
  
// attach the set to the stream
stream.StringSet = set;

// create a new options struct
var options = new StringSetOptions();

/*
For any string whose encoded size (in bytes) is less than or equal to this value, a lookup in
StringSet will be performed before allocating a new string. If the string already exists in
StringSet, then no allocation occurs. If it does not exist in StringSet, or if its encoded size
is larger than this value, then a new string is allocated.

For performance reasons, it is recommended to use a small value, such as 256, or less. Use zero
to disable StringSet lookups altogether.
*/
options.MaxEncodedSizeToLookupInSet = 40;

// make sure StringSet is non-null before calling this
stream.SetDefaultStringOptions(options);

// write the test string
var originalPosition = stream.Position;
stream.WriteString(testString, true);

// read it back
stream.Seek(originalPosition, SeekOrigin.Begin);
var readString = stream.ReadString(true);

// verify that we got the exact same string object
object.ReferenceEquals(testString, readString); // true
```

You can override the default StringSetOptions on each call to ReadString:

```csharp
var options = new StringSetOptions();
options.MaxEncodedSizeToLookupInSet = 0; // disable StringSet

var newString = stream.ReadString(true, setOptions: options);
```

#### Auto-Interning

If your data is coming from a trusted source, you may elect to automatically add new strings to the StringSet. This is means every time you read a string whose encoded size is `MaxEncodedSizeToLookupInSet` or less, if it doesn't already exist in the StringSet, it will be added.

This is a bit dangerous because strings are _never_ purged from StringSet. The strings will never be garbage collected unless the StringSet itself is garbage collected. A user could send you millions of unique strings, causing unbounded memory growth.

For this reason, the option is named `PerformDangerousAutoAddToSet`. Make sure you understand the consequences before enabling it.

```csharp
var options = new StringSetOptions();
options.MaxEncodedSizeToLookupInSet = 40;
options.PerformDangerousAutoAddToSet = true;
```

## StringSet

[StringSet](https://github.com/bretcope/PerformanceTypes/blob/master/PerformanceTypes/StringSet.cs) is a specialized [HashSet](https://msdn.microsoft.com/en-us/library/bb359438%28v=vs.110%29.aspx) for strings. Its primary use case is as an intern pool for parsers because it allows you to extract a substring from char array without re-allocating if that substring has been seen before.

All methods on StringSet are thread-safe. `Add*` methods uses locking only when an existing match is not found. The get/search related methods were carefully designed to be thread-safe without requiring locking or spinning.

There are no "remove" methods. Once a string has been added, it remains in the set until the set itself is garbage collected.

### Initializing

```csharp
var set = new StringSet(INITIAL_SIZE);
```

> You must provide an explicit initial size. The set will double in size every time the current size is exceeded.

### Adding Strings

If you already have the string allocated, you can add it directly:

```csharp
set.Add(myString);
```

If you have a char array buffer, you can add a string to the set by providing the buffer, an offset, and the length of the string. Additionally, there is an `out string str` parameter which is the resulting string. A new string will only be allocated if it does not already exist in the set.

```csharp
string result;
set.Add(myCharArray, start, length, out result);
```

> All overloads of `Add()` return a `bool` which is true if the string was added to the set, and false if the string already existed.

### Searching for Strings in the Set

StringSet provides two different ways to check if a string already exists in the set (without adding it).

The first option is to pass in a char array:

```csharp
var str = set.GetExistingString(charArray, start, length);
// if the string did not exist in the set, str will be null
```

The second is to search for the string by hash code.

```csharp
var hash = StringHash.GetHash(str);
var cursor = set.GetSearchCursor(hash);
while (cursor.MightHaveMore)
{
    if (str == cursor.NextString())
        return true;
}

return false
```

The cursor will only iterate over strings that were present in the set at the time the cursor was created.

> Note that `cursor.MightHaveMore` is exactly what it sounds like. `NextString()` might still return null even if `MightHaveMore` is true. Once `MightHaveMore` becomes false, `NextString()` will _always_ return null.

Although the cursor does maintain a reference to a pre-existing array, the cursor itself is a struct, so performing searches does not incur any heap allocations.

### StringHash

[StringHash](https://github.com/bretcope/PerformanceTypes/blob/master/PerformanceTypes/StringHash.cs) is a supporting type for StringSet operations which represents the [FNV-1a hash](https://en.wikipedia.org/wiki/Fowler%E2%80%93Noll%E2%80%93Vo_hash_function) of a string.

The easiest way to generate the hash is by using one of the static `GetHash()` helper methods.

```csharp
hash = StringHash.GetHash(myString);
hash = StringHash.GetHash(myCharArray, start, length);
```

You can also calculate the hash by manually iterating over the characters of your string.

```csharp
var hash = StringHash.Begin();
foreach (var ch in myString)
{
    hash.Iterate(ch);
}

// hash is now ready for use
```

>  __Never use__ `new StringHash()` or `default(StringHash)` to create the hash. It will not be initialized properly. Use `GetHash()` or `Begin()` instead.

`StringSet.GetSearchCursor()` requires you to pre-calculate the hash of the string you're looking for. Several other methods of StringSet accept an optional StringHash parameter named `knownHashValue`. If you have pre-calculated the hash, you can use this parameter to save unnecessary re-calculation, but be sure you're supplying the correctly calculated hash. An incorrectly calculated hash could result in duplicate strings or make them unsearchable.

### StringSet Use Cases

> __IMPORTANT__: Don't ever add arbitrary user-provided strings to a StringSet. Remember that there is no "remove" functionality. A user could intentionally or unintentionally cause your memory usage to grow, and your application to eventually crash, by sending a large volume of unique strings.

StringSet is primarily intended to support parsers in a production environment where allocations matter (because of garbage collection performance). The most common use case would be to pre-allocate common strings you expect to see, and then use StringSet to avoid allocating a new string every time you parse a common value.

For example, let's say you want to parse a semicolon-delimited list of [Stack Overflow tags](https://stackoverflow.com/tags) (e.g. `c#;java;c++;sql;`). The naïve implementation would be:

```csharp
var tags = tagListString.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
```

There's nothing wrong with that implementation if allocations aren't a big deal for your application. On the other hand, if they _are_ a big deal, here's how you could re-write this parser using StringSet:

```csharp
// StringSet initialization code you would do once
var tagsWeCareExpect = new[] { "c#", "java", "c++" };
var set = new StringSet(tagsWeCareExpect.Length + 10);
foreach (var tag in tagsWeCareExpect)
{
    set.Add(tag);
}

// parsing code
var start = 0;
for (var i = 0; i < buffer.Length; i++)
{
    var ch = buffer[i];

    if (ch == ';')
    {
        var len = i - start;
        var str = set.GetExistingString(buffer, start, len);
        if (str == null)
        {
            // Was not a tag we expected, so we have to allocate a new string.
            // We don't want to add it to the set because this is user input.
            str = new string(buffer, start, len);
        }
        
        // todo - actually do something with this string
        
        start = i + 1;
    }
  
    // todo - handle if the list isn't semi-colon terminated
}
```

Obviously that's a lot more code, but it may be worth it for high-volume parsers.

## UnsafeStringComparer

[UnsafeStringComparer](https://github.com/bretcope/PerformanceTypes/blob/master/PerformanceTypes/UnsafeStringComparer.cs) is a static helper class which helps you perform fast string equality comparisons when one operand is a string and the other is a character buffer. It is called "Unsafe..." because it uses [unsafe](https://msdn.microsoft.com/en-us/library/chfa2zb8.aspx) code for optimizations and some methods accept pointers.

For strings with seven or fewer characters, they are compared one character at a time. For strings with eight or more characters, UnsafeStringComparer will switch to comparing four characters at a time via 64-bit integers, which can result in an almost 4x performance improvement.

> SIMD would likely provide additional performance improvements, but C# does not appear to provide a way to use SIMD with unmanaged pointers. It would have to be done in a native dll, which would make this library less portable.

There is only one method with four public overloads:

```csharp
bool AreEqual(string str, char[] buffer)
```

>  Compares all characters from `str` with all characters in `buffer`.

```csharp
bool AreEqual(string str, char[] buffer, int start, int length)
```

> Compares all characters from `str` with the characters in buffer beginning at the `start` index, and for `length` characters. If `length != str.Length`, or if `start + length > buffer.Length`, the return value will always be false.

```csharp
bool AreEqual(string str, char* buffer, int length)
```

> Compares all the characters from `str` with a character buffer pointed to by `buffer` for `length` characters. If `length != str.Length`, the return value will always be false. It is up to you to ensure that the buffer has at least `length` characters remaining, or this method may read from memory outside your buffer.

```csharp
bool AreEqual(char* aPtr, char* bPtr, int length)
```

> Compares characters from two buffers pointed at by `aPtr` and `bPtr` for `length` characters. It is up to you to ensure both buffers have at least `length` characters remaining, or this method may read from memory outside your buffers.

## UnsafeMd5

The MD5 methods in the BCL allocate a byte array in order to return the hash value. The methods on UnsafeMd5 return the hash as an `Md5Digest` struct, and therefore avoid any heap allocations. This implementation will out-perform the BCL implementation for small buffer sizes (~700 bytes or less), but is a slower on larger buffers.

>  __Note: UnsafeMd5 will only return the accurate MD5 hash on little endian architectures (such as Intel x86).__

```csharp
Md5Digest digest;

// managed interface
UnsafeMd5.ComputeHash(byteArray, out digest);

// unmanaged interface
UnsafeMd5.ComputeHash(bytePtr, length, &digest);
```

The Md5Digest struct is 16 bytes (the size of an MD5 hash). This constant is also available via `Md5Digest.SIZE`.

There are a few different ways to get the actual bytes from the digest struct.

```csharp
// cast it as a byte pointer
var bytesPtr = (byte*)&digest;

// copy the bytes into a managed byte[] buffer starting at index
digest.WriteBytes(buffer, index);

// copy bytes to an unmanaged buffer (make sure at least 16 bytes are available)
digest.WriteBytes(bufferPtr);

// call GetBytes to simply allocate a new byte[]
var bytes = digest.GetBytes();

// call ToString to get a hex representation of the hash (obviously this allocates a string)
var hex = digest.ToString(); // example: 1f2cc2829f9ec439fab4f45ab54d8a82
```

Md5Digest also implements `IEquatable<Md5Digest>` for comparison purposes and offers operator overloads for `==` and `!=`.

## Unsafe

A small static utilities class for unsafe operations.

### MemoryCopy

This is not a general-purpose replacement for `memcpy` or methods like `Marshal.Copy`. However, it is a fast alternative for small data buffers. In my testing, it will out-perform the alternatives for buffer sizes of around 400 bytes or less.

```csharp
Unsafe.MemoryCopy(srcPtr, destPtr, bytesCount);
```

### ToHexString

Takes an unmanaged byte pointer and length (in bytes), and returns a hexadecimal representation of the data. The string itself is the only heap allocation this method makes. There are no intermediate objects.

```csharp
var bytes = stackalloc byte[2];
bytes[0] = 0xa5;
bytes[1] = 0xf7;

var str = ToHexString(bytes, 2); // "a5f7"
```

