#version 330 core

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

const float PI = 3.14159265359;

#define MAX_DIR_LIGHTS 32
#define MAX_POINT_LIGHTS 32
#define MAX_SPOT_LIGHTS 32

#define MAX_SHADOW_POINT_LIGHTS 4
#define MAX_SHADOW_SPOT_LIGHTS 4

struct DirLight {
    vec3 direction;
    vec3 color;
    float intensity;
};

struct PointLight {
    vec3 position;
    vec3 color;
    float intensity;
    float constant;
    float linear;
    float quadratic;
};

struct SpotLight {
    vec3 position;
    vec3 direction;
    vec3 color;
    float intensity;
    float innerCutOff;
    float outerCutOff;
    float constant;
    float linear;
    float quadratic;
};

struct MaterialPBR {
    vec3 albedo;
    float metallic;
    float roughness;
    float ao;
};

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
uniform float u_Alpha;

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

uniform samplerCube u_PointShadowMaps[MAX_SHADOW_POINT_LIGHTS];
uniform sampler2D u_SpotShadowMap;

uniform mat4 u_LightSpaceMatrix;
uniform mat4 u_SpotLightSpaceMatrices[MAX_SHADOW_SPOT_LIGHTS];

uniform float u_PointLightFarPlanes[MAX_SHADOW_POINT_LIGHTS];
uniform int u_NumShadowPointLights;

float DistributionGGX(vec3 N, vec3 H, float roughness)
{
    float a = roughness * roughness;
    float a2 = a * a;
    float NdotH = max(dot(N, H), 0.0);
    float denom = (NdotH * NdotH * (a2 - 1.0) + 1.0);
    return a2 / max(PI * denom * denom, 0.0001);
}

float GeometrySchlickGGX(float NdotV, float roughness)
{
    float r = roughness + 1.0;
    float k = (r * r) / 8.0;
    return NdotV / max(NdotV * (1.0 - k) + k, 0.0001);
}

float GeometrySmith(vec3 N, vec3 V, vec3 L, float roughness)
{
    return GeometrySchlickGGX(max(dot(N, V), 0.0), roughness)
         * GeometrySchlickGGX(max(dot(N, L), 0.0), roughness);
}

vec3 fresnelSchlick(float cosTheta, vec3 F0)
{
    return F0 + (1.0 - F0) * pow(clamp(1.0 - cosTheta, 0.0, 1.0), 5.0);
}

vec3 fresnelSchlickRoughness(float cosTheta, vec3 F0, float roughness)
{
    return F0 + (max(vec3(1.0 - roughness), F0) - F0) * pow(clamp(1.0 - cosTheta, 0.0, 1.0), 5.0);
}

vec3 CalcDirLightPBR(DirLight light, vec3 N, vec3 V, vec3 F0,
                     vec3 albedo, float metallic, float roughness)
{
    vec3 L = normalize(-light.direction);
    vec3 H = normalize(V + L);
    vec3 F = fresnelSchlick(max(dot(H, V), 0.0), F0);
    vec3 spec = (DistributionGGX(N, H, roughness) * GeometrySmith(N, V, L, roughness) * F)
               / (4.0 * max(dot(N, V), 0.0) * max(dot(N, L), 0.0) + 0.0001);
    vec3 kD = (vec3(1.0) - F) * (1.0 - metallic);
    return (kD * albedo / PI + spec) * light.color * light.intensity * max(dot(N, L), 0.0);
}

vec3 CalcPointLightPBR(PointLight light, vec3 N, vec3 V, vec3 F0,
                       vec3 albedo, float metallic, float roughness, vec3 fragPos)
{
    vec3 L = normalize(light.position - fragPos);
    vec3 H = normalize(V + L);
    float dist = length(light.position - fragPos);
    float att = 1.0 / (light.constant + light.linear * dist + light.quadratic * dist * dist);
    vec3 F = fresnelSchlick(max(dot(H, V), 0.0), F0);
    vec3 spec = (DistributionGGX(N, H, roughness) * GeometrySmith(N, V, L, roughness) * F)
               / (4.0 * max(dot(N, V), 0.0) * max(dot(N, L), 0.0) + 0.0001);
    vec3 kD = (vec3(1.0) - F) * (1.0 - metallic);
    return (kD * albedo / PI + spec) * light.color * light.intensity * att * max(dot(N, L), 0.0);
}

vec3 CalcSpotLightPBR(SpotLight light, vec3 N, vec3 V, vec3 F0,
                      vec3 albedo, float metallic, float roughness, vec3 fragPos)
{
    vec3 L = normalize(light.position - fragPos);
    vec3 H = normalize(V + L);
    float dist = length(light.position - fragPos);
    float att = 1.0 / (light.constant + light.linear * dist + light.quadratic * dist * dist);
    float theta = dot(L, normalize(-light.direction));
    float spotI = clamp((theta - light.outerCutOff) / (light.innerCutOff - light.outerCutOff), 0.0, 1.0);
    vec3 F = fresnelSchlick(max(dot(H, V), 0.0), F0);
    vec3 spec = (DistributionGGX(N, H, roughness) * GeometrySmith(N, V, L, roughness) * F)
                  / (4.0 * max(dot(N, V), 0.0) * max(dot(N, L), 0.0) + 0.0001);
    vec3 kD = (vec3(1.0) - F) * (1.0 - metallic);
    return (kD * albedo / PI + spec) * light.color * light.intensity * att * spotI * max(dot(N, L), 0.0);
}

vec3 CalcAmbientPBR(vec3 albedo, vec3 N, vec3 V, vec3 F0,
                    float roughness, float metallic, float ao,
                    vec3 ambientLight, float ambientStrength)
{
    vec3 F = fresnelSchlickRoughness(max(dot(N, V), 0.0), F0, roughness);
    vec3 kD = (1.0 - F) * (1.0 - metallic);
    // Diffuse ambient + specular ambient aproximado (sin IBL)
    vec3 diffuse = kD * albedo;
    vec3 specular = F * 0.1;
    return (diffuse + specular) * ambientLight * ao * ambientStrength;
}

vec3 ApplyToneMapping(vec3 color)
{
    color = color / (color + vec3(1.0));
    return pow(color, vec3(1.0 / 2.2));
}

float CalcPointShadow(int idx, vec3 fragPos, vec3 lightPos, vec3 normal)
{
    vec3 toLight = fragPos - lightPos;
    float dist = length(toLight);
    float farPlane = u_PointLightFarPlanes[idx];
    
    vec3 dir = normalize(toLight);
    float closestDist = texture(u_PointShadowMaps[idx], dir).r;
    float currentDist = dist / farPlane;

    float baseBias = 0.02;
    float angleFactor = clamp(1.0 - dot(normal, -dir), 0.0, 1.0);
    float bias = baseBias + angleFactor * 0.05;

    // If current distance is much further than stored, we're in shadow
    if (currentDist - bias > closestDist)
        return 1.0;
    else
        return 0.0;
}

float CalcSpotShadow(int idx, vec3 fragPos)
{
    vec4 fragPosLS = u_SpotLightSpaceMatrices[idx] * vec4(fragPos, 1.0);

    if (fragPosLS.w <= 0.0) return 0.0;

    vec3 projCoords = fragPosLS.xyz / fragPosLS.w;
    projCoords = projCoords * 0.5 + 0.5;

    if (projCoords.z <= 0.0 || projCoords.z >= 1.0) return 0.0;
    if (projCoords.x < 0.0 || projCoords.x > 1.0 ||
        projCoords.y < 0.0 || projCoords.y > 1.0) return 0.0;

    float closestDepth = texture(u_SpotShadowMap, projCoords.xy).r;
    float currentDepth = projCoords.z;
    float bias = 0.005;

    float shadow = (currentDepth - bias) > closestDepth ? 1.0 : 0.0;
    
    return shadow;
}

vec3 getNormalFromMap()
{
    if (u_UseNormalMap == 0) return normalize(Normal);
    vec3 t = texture(u_NormalMap, TexCoord).xyz * 2.0 - 1.0;
    t.xy *= u_NormalMapIntensity;
    return normalize(TBN * normalize(t));
}

void main()
{
    float alpha = (u_UseAlbedoMap == 1) ? texture(u_AlbedoMap, TexCoord).a : 1.0;
    alpha *= u_Alpha;
    if (alpha < 0.01) discard;

    MaterialPBR material;
    material.albedo = (u_UseAlbedoMap == 1) ? pow(texture(u_AlbedoMap, TexCoord).rgb, vec3(2.2)) : u_AlbedoColor;
    material.metallic = (u_UseMetallicMap == 1) ? texture(u_MetallicMap, TexCoord).r : u_Metallic;
    material.roughness = (u_UseRoughnessMap == 1) ? texture(u_RoughnessMap, TexCoord).r : u_Roughness;
    material.ao = (u_UseAOMap == 1) ? texture(u_AOMap, TexCoord).r : u_AO;

    vec3 emissive = (u_UseEmissiveMap == 1) ? texture(u_EmissiveMap, TexCoord).rgb : u_EmissiveColor;

    vec3 N = getNormalFromMap();
    vec3 V = normalize(u_CameraPos - FragPos);
    vec3 F0 = mix(vec3(0.04), material.albedo, material.metallic);

    vec3 Lo = vec3(0.0);

    for (int i = 0; i < numDirLights; i++)
    {
        Lo += CalcDirLightPBR(dirLights[i], N, V, F0,
                              material.albedo, material.metallic, material.roughness);
    }

    for (int i = 0; i < numPointLights; i++)
    {
        float shadow = 0.0;
        if (i < u_NumShadowPointLights)
        {
            shadow = CalcPointShadow(i, FragPos, pointLights[i].position, N);
        }
        Lo += CalcPointLightPBR(pointLights[i], N, V, F0,
                                material.albedo, material.metallic, material.roughness, FragPos)
            * (1.0 - shadow);
    }

    for (int i = 0; i < numSpotLights; i++)
    {
        float shadow = 0.0;
        if (i < MAX_SHADOW_SPOT_LIGHTS)
        {
            shadow = CalcSpotShadow(i, FragPos);
        }
        Lo += CalcSpotLightPBR(spotLights[i], N, V, F0,
                               material.albedo, material.metallic, material.roughness, FragPos)
            * (1.0 - shadow);
    }

    vec3 ambient = CalcAmbientPBR(material.albedo, N, V, F0,
                                  material.roughness, material.metallic, material.ao,
                                  u_AmbientLight, u_AmbientStrength);

    vec3 color = ApplyToneMapping(ambient + Lo);

    FragColor = vec4(color, alpha);
    EmissionColor = vec4(emissive, alpha);
    PositionOutput = ViewPos;
    NormalOutput = normalize(mat3(u_View) * N);
}
