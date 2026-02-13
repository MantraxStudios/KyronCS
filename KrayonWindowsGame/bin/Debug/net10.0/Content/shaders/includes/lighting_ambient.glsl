#ifndef LIGHTING_AMBIENT_GLSL
#define LIGHTING_AMBIENT_GLSL

vec3 CalcAmbientPBR(vec3 albedo, vec3 N, vec3 V, vec3 F0, float roughness, float metallic, float ao, vec3 ambientLight, float ambientStrength)
{
    vec3 F = fresnelSchlickRoughness(max(dot(N, V), 0.0), F0, roughness);
    vec3 kS = F;
    vec3 kD = 1.0 - kS;
    kD *= 1.0 - metallic;
    vec3 ambient = ambientLight * albedo * ao * ambientStrength;
    vec3 ambientSpecular = F * roughness * 0.1;
    ambient += ambientSpecular * ao;
    return ambient;
}

#endif