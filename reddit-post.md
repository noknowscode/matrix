# Made Matrix rain react to mouse input and can't stop playing with it

What started as a page transition turned into per-character scatter physics. Elliptical influence zones, quadratic falloff, sin-based noise so it doesn't look too uniform. Each character calculates its own displacement from the cursor every frame.

The whole thing came together faster than expected - Uno Platform + SkiaSharp, iterating in a tight loop until it felt right.

[video]
