# Semver
![Nuget](https://img.shields.io/nuget/v/Acklann.Semver)
![GitHub](https://img.shields.io/github/license/Ackara/Semver)
---

A comprehensive netstandard implementation of Semantic Version (https://semver.org/).

## Features

### Implicit string conversion

```csharp
SemanticVersion version = "1.2.3-beta+190814";
string nextVersion = new SemanticVersion(1, 2, 3);
```


### Comparison operators

```csharp
var a = new SemanticVersion(0, 0, 1);
var b = new SemanticVersion(1, 2, 3);

a == b // False
a < b // True
a > b // False
```


### String Formatting

```csharp
SemanticVersion ver = "1.2.3-beta+456";

ver.ToString("C"); // returns:  1.2.3
ver.ToString("x.y.z_p"); // returns:  1.2.3_beta
ver.ToString("x.y.z-p+YYMMddHHmm"); // returns:  1.2.3.beta+1908280823
```

| Specifier | Description | Example |
|-----------|-------------|---------|
| G | The standard string format | 1.2.3-beta-456 |
| g | The standard format without build | 1.2.3-beta |
| C | The standard format without pre-release and build | 1.2.3 |
| x | The *major* number | 1 |
| y | The *minor* number | 2 |
| z | The *patch* number | 3 |
| p | The *pre-release* number | beta |
| b | The *build* number | 456 |
| \ | Chracter escape |  |

---

The format string can accept basic `DateTime.UtcNow` format specifiers as well.

`DateTime.UtcNow = 2019-08-14 11:23:09.122 AM`

| Specifier | Description | Example |
|-----------|-------------|---------|
| YY | The current year | 19 |
| MM | The current month | 08 |
| dd | The current day | 14 |
| HH | The current hour | 11 |
| mm | The current minute | 23 |
| ss | The current second | 09 |
| ff | The current millisecond | 122 |
