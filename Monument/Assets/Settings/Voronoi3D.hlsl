// High-quality pseudo-random 3D hash
float3 hash33(float3 p)
{
    p = frac(p * float3(0.1031, 0.1030, 0.0973));
    p += dot(p, p.yxz + 33.33);
    return frac((p.xxy + p.yxx) * p.zyx);
}

void Voronoi3D_float(
    float3 Position,
    float CellDensity,
    float AngleOffset,
    out float Out,
    out float Cells)
{
    float3 g = floor(Position * CellDensity);
    float3 f = frac(Position * CellDensity);
    float minDist = 8.0;
    float3 targetCell = float3(0, 0, 0);

    // Search 3x3x3 neighboring cells
    for (int z = -1; z <= 1; z++)
    {
        for (int y = -1; y <= 1; y++)
        {
            for (int x = -1; x <= 1; x++)
            {
                float3 lattice = float3(x, y, z);
                float3 h = hash33(g + lattice);
                
                // Animate or shift point inside cell
                float3 offset = 0.5 + 0.5 * sin(AngleOffset + 6.2831853 * h);
                float3 r = lattice + offset - f;
                float d = dot(r, r);

                if (d < minDist)
                {
                    minDist = d;
                    targetCell = g + lattice;
                }
            }
        }
    }

    Out = sqrt(minDist);
    // Random float per cell for stepped/cell shading
    Cells = frac(sin(dot(targetCell, float3(12.9898, 78.233, 45.164))) * 43758.5453);
}