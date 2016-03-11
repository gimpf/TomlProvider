TomlProvider - an F# type provider for TOML files
=================================================

`TomlProvider` uses [Nett](https://github.com/paiden/Nett), a
TOML library written in C#, and wraps an erasing type provider
around it.

While TOML can be misused for data storage, the primary purpose
is to have a convenient way to specify a configuration file
format and at the same time generate a compile-time checked
API for accessing such.

License
-------

[CC0 1.0 Universal](https://creativecommons.org/publicdomain/zero/1.0/)
