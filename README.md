JSON library that supports streaming including properties holding base64 data.

Only the async methods are supported.

Updates:

1.0.1 - Fixes to async code to avoid dependency on StreamReader Peek/EndOfStream
        methods which cause non-async calls to be made.