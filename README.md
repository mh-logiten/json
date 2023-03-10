JSON library that supports streaming including properties holding base64 data.

Only the async methods are supported.

Updates:

1.0.4 - Fix issue that allowed reading past the end of the string in
        Base64StringStream. Fix nuget dependency warning.

1.0.3 - Fix issue in JsonReader when skipping string/base64 properties.

1.0.2 - Fix to overcome bug in .net core 3.1 FromBase64Transform where
        InputBlockSize was set to 1 rather than the value 4 (4 bytes input,
        3 bytes output).

1.0.1 - Fixes to async code to avoid dependency on StreamReader Peek/EndOfStream
        methods which cause non-async calls to be made.
