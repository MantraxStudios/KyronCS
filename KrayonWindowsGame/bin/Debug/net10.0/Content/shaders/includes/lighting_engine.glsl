#ifndef LIGHTING_ENGINE_GLSL
#define LIGHTING_ENGINE_GLSL

struct MaterialPBR {
    vec3 albedo;
    float metallic;
    float roughness;
    float ao;
};

struct LightingContext {
    vec3 N;
    vec3 V;
    vec3 F0;
    vec3 fragPos;
};

vec3 CalculatePBRLighting(
    MaterialPBR material,
    LightingContext ctx,
    DirLight dirLights[MAX_DIR_LIGHTS],
    PointLight pointLights[MAX_POINT_LIGHTS],
    SpotLight spotLights[MAX_SPOT_LIGHTS],
    int numDirLights,
    int numPointLights,
    int numSpotLights,
    vec3 ambientLight,
    float ambientStrength
)
{
    vec3 Lo = vec3(0.0);
    
    for(int i = 0; i < numDirLights; i++)
        Lo += CalcDirLightPBR(dirLights[i], ctx.N, ctx.V, ctx.F0, material.albedo, material.metallic, material.roughness);
    
    for(int i = 0; i < numPointLights; i++)
        Lo += CalcPointLightPBR(pointLights[i], ctx.N, ctx.V, ctx.F0, material.albedo, material.metallic, material.roughness, ctx.fragPos);
    
    for(int i = 0; i < numSpotLights; i++)
        Lo += CalcSpotLightPBR(spotLights[i], ctx.N, ctx.V, ctx.F0, material.albedo, material.metallic, material.roughness, ctx.fragPos);
    
    vec3 ambient = CalcAmbientPBR(material.albedo, ctx.N, ctx.V, ctx.F0, material.roughness, material.metallic, material.ao, ambientLight, ambientStrength);
    
    return ambient + Lo;
}

vec3 ApplyToneMapping(vec3 color)
{
    color = color / (color + vec3(1.0));
    color = pow(color, vec3(1.0 / 2.2));
    return color;
}

#endif