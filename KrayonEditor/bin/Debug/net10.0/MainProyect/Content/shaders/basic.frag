#version 330 core

#include "includes/lighting_common.glsl"
#include "includes/lighting_pbr.glsl"
#include "includes/lighting_ambient.glsl"
#include "includes/lighting_engine.glsl"

layout(location = 0) out vec4 FragColor;
layout(location = 1) out vec4 EmissionColor;
layout(location = 2) out vec3 PositionOutput;
layout(location = 3) out vec3 NormalOutput;

in vec3 FragPos;
in vec3 ViewPos;
in vec3 Normal;
in vec3 ViewNormal;
in vec2 TexCoord;
in mat3 TBN;

uniform vec3 u_AlbedoColor;
uniform float u_Metallic;
uniform float u_Roughness;
uniform float u_AO;
uniform vec3 u_EmissiveColor;
uniform float u_NormalMapIntensity;

uniform sampler2D u_AlbedoMap;
uniform sampler2D u_NormalMap;
uniform sampler2D u_MetallicMap;
uniform sampler2D u_RoughnessMap;
uniform sampler2D u_AOMap;
uniform sampler2D u_EmissiveMap;

uniform int u_UseAlbedoMap;
uniform int u_UseNormalMap;
uniform int u_UseMetallicMap;
uniform int u_UseRoughnessMap;
uniform int u_UseAOMap;
uniform int u_UseEmissiveMap;
uniform float u_Alpha = 1.0;

uniform vec3 u_CameraPos;
uniform vec3 u_AmbientLight;
uniform float u_AmbientStrength;

uniform mat4 u_View;

uniform DirLight dirLights[MAX_DIR_LIGHTS];
uniform PointLight pointLights[MAX_POINT_LIGHTS];
uniform SpotLight spotLights[MAX_SPOT_LIGHTS];

uniform int numDirLights;
uniform int numPointLights;
uniform int numSpotLights;

vec3 getNormalFromMap()
{
    if(u_UseNormalMap == 0)
        return normalize(Normal);
    vec3 tangentNormal = texture(u_NormalMap, TexCoord).xyz * 2.0 - 1.0;
    tangentNormal.xy *= u_NormalMapIntensity;
    tangentNormal = normalize(tangentNormal);
    return normalize(TBN * tangentNormal);
}

void main()
{
    // Sacar el alpha por separado
    float alpha = u_UseAlbedoMap == 1 ? texture(u_AlbedoMap, TexCoord).a : 1.0;
    
    // Multiplicar por alpha uniforme si tienes uno (opcional)
    alpha *= u_Alpha; // uniform float u_Alpha = 1.0 por defecto
    
    MaterialPBR material;
    material.albedo = u_UseAlbedoMap == 1 ? pow(texture(u_AlbedoMap, TexCoord).rgb, vec3(2.2)) : u_AlbedoColor;
    material.metallic = u_UseMetallicMap == 1 ? texture(u_MetallicMap, TexCoord).r : u_Metallic;
    material.roughness = u_UseRoughnessMap == 1 ? texture(u_RoughnessMap, TexCoord).r : u_Roughness;
    material.ao = u_UseAOMap == 1 ? texture(u_AOMap, TexCoord).r : u_AO;
    
    vec3 emissive = u_UseEmissiveMap == 1 ? texture(u_EmissiveMap, TexCoord).rgb : u_EmissiveColor;
    
    LightingContext ctx;
    ctx.N = getNormalFromMap();
    ctx.V = normalize(u_CameraPos - FragPos);
    ctx.F0 = mix(vec3(0.04), material.albedo, material.metallic);
    ctx.fragPos = FragPos;
    
    vec3 color = CalculatePBRLighting(
        material, ctx,
        dirLights, pointLights, spotLights,
        numDirLights, numPointLights, numSpotLights,
        u_AmbientLight, u_AmbientStrength
    );
    
    color = ApplyToneMapping(color);
    
    // Discard si alpha es muy bajo (opcional, para cutout)
    if (alpha < 0.01) discard;
    
    FragColor = vec4(color, alpha); // <-- alpha aquí
    EmissionColor = vec4(emissive, alpha);
    
    mat3 viewNormalMatrix = mat3(u_View);
    vec3 viewSpaceNormal = normalize(viewNormalMatrix * ctx.N);
    
    PositionOutput = ViewPos;
    NormalOutput = viewSpaceNormal;
}