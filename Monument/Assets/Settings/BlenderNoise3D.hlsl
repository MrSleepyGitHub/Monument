float hash31(float3 p)
{
    p = frac(p * 0.3183099 + 0.1);
    p *= 17.0;
    return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
}

// 3D Value/Gradient noise base layer
float noise3D(float3 x)
{
    float3 i = floor(x);
    float3 f = frac(x);
    // Quintic smoothstep interpolation curve
    f = f * f * f * (f * (f * 6.0 - 15.0) + 10.0);

    return lerp(
        lerp(
            lerp(hash31(i + float3(0,0,0)), hash31(i + float3(1,0,0)), f.x),
            lerp(hash31(i + float3(0,1,0)), hash31(i + float3(1,1,0)), f.x), f.y),
        lerp(
            lerp(hash31(i + float3(0,0,1)), hash31(i + float3(1,0,1)), f.x),
            lerp(hash31(i + float3(0,1,1)), hash31(i + float3(1,1,1)), f.x), f.y),
        f.z);
}

void BlenderNoise3D_float(
    float3 Position,
    float Scale,
    float Detail,
    float Roughness,
    float Lacunarity,
    out float Out)
{
    float3 p = Position * Scale;
    float value = 0.0;
    float amplitude = 1.0;
    float totalAmp = 0.0;
    int octaves = clamp((int)Detail, 1, 8);

    for (int i = 0; i < octaves; i++)
    {
        value += amplitude * noise3D(p);
        totalAmp += amplitude;
        p *= Lacunarity;
        amplitude *= Roughness;
    }

    Out = value / totalAmp;
}