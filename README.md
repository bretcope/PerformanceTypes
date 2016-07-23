# Performance Types

This library is a small collection of specialized helper types which focus on performance and reducing allocations.

- [StopwatchStruct](#stopwatchstruct)
- [StringSet](#stringset)
  - [StringHash](#stringhash)

## StopwatchStruct

[StopwatchStruct](https://github.com/bretcope/PerformanceTypes/blob/master/PerformanceTypes/StopwatchStruct.cs) is a partial re-implementation of the [Stopwatch](https://msdn.microsoft.com/en-us/library/system.diagnostics.stopwatch) class with methods for `Start()`, `Stop()`, and `GetElapsedMilliseconds()` plus an `ElapsedTicks` property.

The main advantage over the Stopwatch class is that StopwatchStruct is a value type, and therefore can be allocated on the stack, where it won't incur any garbage collection 

```csharp
var sw = new StopwatchStruct();
sw.Start();
DoSomething();
sw.Stop();

double duration = sw.GetElapsedMilliseconds();
```

>  StopwatchStruct is intended to be started and stopped within a single method. It's generally not recommended to pass the struct to other methods or return it unless you understand all the implications of pass-by-value for a mutable struct.

## StringSet

[StringSet](https://github.com/bretcope/PerformanceTypes/blob/master/PerformanceTypes/StringSet.cs) is a specialized [HashSet](https://msdn.microsoft.com/en-us/library/bb359438%28v=vs.110%29.aspx) for strings. Its primary use case is as an intern pool for parsers because it allows you to extract a substring from char array without re-allocating if that substring has been seen before.

All methods on StringSet are thread-safe. `Add*` methods uses locking only when an existing match is not found. The get/search related methods were carefully designed to be thread-safe without requiring locking or spinning.

There are no "remove" methods. Once a string has been added, it remains in the set until the set itself is garbage collected.

#### Initializing

```csharp
var set = new StringSet(INITIAL_SIZE);
```

> You must provide an explicit initial size. The set will double in size every time the current size is exceeded.

#### Adding Strings

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

#### Searching for Strings in the Set

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

#### StringHash

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

#### StringSet Use Cases

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

