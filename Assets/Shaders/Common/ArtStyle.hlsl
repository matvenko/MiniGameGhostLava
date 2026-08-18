#ifndef GHOSTLAVA_ART_STYLE_INCLUDED
#define GHOSTLAVA_ART_STYLE_INCLUDED

// The scene's shared visual language. Every surface shader includes this and
// lights itself through ArtCelLight, so the ground, the wall and anything added
// later respond to light identically.
//
// This exists because the alternative was tried and failed: ground, wall and
// water each had their own hand-written lighting, and no amount of per-material
// tuning could make them look like the same world - they were not solving the
// same equation. Consistency has to be structural, not a tuning exercise.
//
// Surface colour comes from flat palette bands, never from a texture. Which band
// a facet falls into is baked into its vertex colours by the mesh builders, so
// the noise that picks the bands lives in C# where it can be inspected, and the
// shader only ever ramps and lights it.

// Three-stop palette ramp. Fed a value that was already quantised on the CPU it
// yields flat bands; the two lerps only interpolate between adjacent stops.
half3 ArtRamp3(half3 a, half3 b, half3 c, half t)
{
    return t < 0.5h ? lerp(a, b, t * 2.0h) : lerp(b, c, (t - 0.5h) * 2.0h);
}

// Quantise with a softened step edge, so the boundaries do not crawl with
// aliasing where the surface turns away from the light.
half ArtPosterize(half v, half steps, half soft)
{
    half q = v * steps;
    half f = frac(q);
    return (floor(q) + smoothstep(0.5h - soft, 0.5h + soft, f)) / steps;
}

// Facet normals swing by up to ~25 degrees on the low-poly ground. Lit directly
// under banded light, neighbouring triangles land in different bands and the
// surface shatters into visible triangles. Pulling the normal back towards the
// face's own axis keeps the banding calm; the facets still read, through their
// baked colour rather than through the light.
float3 ArtFacetNormal(float3 n, float3 faceAxis, float flatten)
{
    return normalize(lerp(n, faceAxis, flatten));
}

// Half-lambert, then banded. Half-lambert rather than plain N.L because a fully
// unlit facet reads as a hole in a flat-shaded surface.
half ArtCelLight(float3 n, float3 lightDir, half steps, half celStrength)
{
    half wrapped = saturate(dot(n, lightDir)) * 0.5h + 0.5h;
    wrapped *= wrapped;
    return lerp(wrapped, ArtPosterize(wrapped, steps, 0.12h), celStrength);
}

// Cheap hash for the few things still generated per pixel, such as flower heads.
float ArtHash21(float2 p)
{
    p = frac(p * float2(123.34, 456.21));
    p += dot(p, p + 45.32);
    return frac(p.x * p.y);
}

#endif
