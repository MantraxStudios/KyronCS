#version 330 core

out vec4 FragColor;

in vec3 FragPos;
in vec3 Normal;
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

uniform vec3 u_CameraPos;
uniform vec3 u_AmbientLight;
uniform float u_AmbientStrength;

const float PI = 3.14159265359;

vec3 lightPositions[4] = vec3[](
    vec3(10.0, 10.0, 10.0),
    vec3(-10.0, 10.0, 10.0),
    vec3(10.0, -10.0, 10.0),
    vec3(-10.0, -10.0, 10.0)
);

vec3 lightColors[4] = vec3[](
    vec3(23.47, 21.31, 20.79),
    vec3(15.0, 15.0, 20.0),
    vec3(10.0, 10.0, 10.0),
    vec3(8.0, 8.0, 12.0)
);

vec3 getNormalFromMap()
{
    if(u_UseNormalMap == 0)
        return normalize(Normal);
    
    vec3 tangentNormal = texture(u_NormalMap, TexCoord).xyz * 2.0 - 1.0;
    tangentNormal.xy *= u_NormalMapIntensity;
    tangentNormal = normalize(tangentNormal);
    
    return normalize(TBN * tangentNormal);
}

float DistributionGGX(vec3 N, vec3 H, float roughness)
{
    float a = roughness * roughness;
    float a2 = a * a;
    float NdotH = max(dot(N, H), 0.0);
    float NdotH2 = NdotH * NdotH;
    
    float nom = a2;
    float denom = (NdotH2 * (a2 - 1.0) + 1.0);
    denom = PI * denom * denom;
    
    return nom / max(denom, 0.0001);
}

float GeometrySchlickGGX(float NdotV, float roughness)
{
    float r = (roughness + 1.0);
    float k = (r * r) / 8.0;
    
    float nom = NdotV;
    float denom = NdotV * (1.0 - k) + k;
    
    return nom / max(denom, 0.0001);
}

float GeometrySmith(vec3 N, vec3 V, vec3 L, float roughness)
{
    float NdotV = max(dot(N, V), 0.0);
    float NdotL = max(dot(N, L), 0.0);
    float ggx2 = GeometrySchlickGGX(NdotV, roughness);
    float ggx1 = GeometrySchlickGGX(NdotL, roughness);
    
    return ggx1 * ggx2;
}

vec3 fresnelSchlick(float cosTheta, vec3 F0)
{
    return F0 + (1.0 - F0) * pow(clamp(1.0 - cosTheta, 0.0, 1.0), 5.0);
}

vec3 fresnelSchlickRoughness(float cosTheta, vec3 F0, float roughness)
{
    return F0 + (max(vec3(1.0 - roughness), F0) - F0) * pow(clamp(1.0 - cosTheta, 0.0, 1.0), 5.0);
}

void main()
{
    vec3 albedo;
    if(u_UseAlbedoMap == 1) {
        albedo = pow(texture(u_AlbedoMap, TexCoord).rgb, vec3(2.2));
    } else {
        albedo = u_AlbedoColor;
    }
    
    float metallic;
    if(u_UseMetallicMap == 1) {
        metallic = texture(u_MetallicMap, TexCoord).r;
    } else {
        metallic = u_Metallic;
    }
    
    float roughness;
    if(u_UseRoughnessMap == 1) {
        roughness = texture(u_RoughnessMap, TexCoord).r;
    } else {
        roughness = u_Roughness;
    }
    
    float ao;
    if(u_UseAOMap == 1) {
        ao = texture(u_AOMap, TexCoord).r;
    } else {
        ao = u_AO;
    }
    
    vec3 emissive;
    if(u_UseEmissiveMap == 1) {
        emissive = texture(u_EmissiveMap, TexCoord).rgb;
    } else {
        emissive = u_EmissiveColor;
    }
    
    vec3 N = getNormalFromMap();
    vec3 V = normalize(u_CameraPos - FragPos);
    
    vec3 F0 = vec3(0.04);
    F0 = mix(F0, albedo, metallic);
    
    vec3 Lo = vec3(0.0);
    
    for(int i = 0; i < 4; ++i)
    {
        vec3 L = normalize(lightPositions[i] - FragPos);
        vec3 H = normalize(V + L);
        float distance = length(lightPositions[i] - FragPos);
        
        float attenuation = 1.0 / (1.0 + 0.09 * distance + 0.032 * distance * distance);
        vec3 radiance = lightColors[i] * attenuation;
        
        float NDF = DistributionGGX(N, H, roughness);
        float G = GeometrySmith(N, V, L, roughness);
        vec3 F = fresnelSchlick(max(dot(H, V), 0.0), F0);
        
        vec3 numerator = NDF * G * F;
        float denominator = 4.0 * max(dot(N, V), 0.0) * max(dot(N, L), 0.0) + 0.0001;
        vec3 specular = numerator / denominator;
        
        vec3 kS = F;
        vec3 kD = vec3(1.0) - kS;
        kD *= 1.0 - metallic;
        
        float NdotL = max(dot(N, L), 0.0);
        
        Lo += (kD * albedo / PI + specular) * radiance * NdotL;
    }
    
    vec3 F = fresnelSchlickRoughness(max(dot(N, V), 0.0), F0, roughness);
    vec3 kS = F;
    vec3 kD = 1.0 - kS;
    kD *= 1.0 - metallic;
    
    vec3 ambient = u_AmbientLight * albedo * ao * u_AmbientStrength;
    vec3 ambientSpecular = F * roughness * 0.1;
    ambient += ambientSpecular * ao;
    
    vec3 color = ambient + Lo + emissive;
    
    color = color / (color + vec3(1.0));
    color = pow(color, vec3(1.0 / 2.2));
    
    FragColor = vec4(color, 1.0);
}