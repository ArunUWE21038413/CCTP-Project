Shader "Custom/BlendShader" {
    Properties {
        // Texture Maps
        _MainTex ("Base Texture", 2D) = "white" {}
        _NormalMap ("Normal Map", 2D) = "bump" {}
        _EmissionMap ("Emission Map", 2D) = "black" {}

        // Colors
        [HDR] _EmissionColor ("Emission Color", Color) = (0, 0, 0, 0)
        _AlbedoColor ("Albedo Color", Color) = (1, 1, 1, 1)

        // Blend Factor
        _Blend ("Blend Factor", Range(0, 1)) = 0.5
    }

    SubShader {
        Tags { "RenderType" = "Opaque" }
        LOD 200

        CGPROGRAM
        // Include surface shading model and enable shadows
        #pragma surface surf Standard fullforwardshadows
        #pragma target 3.0

        // Texture samplers
        sampler2D _MainTex;
        sampler2D _NormalMap;
        sampler2D _EmissionMap;

        // Material properties
        fixed4 _EmissionColor;
        fixed4 _AlbedoColor;
        half _Blend;

        struct Input {
            float2 uv_MainTex;      // UVs for the main texture
            float2 uv_NormalMap;    // UVs for the normal map
            float2 uv_EmissionMap;  // UVs for the emission map
        };

        // Surface function: defines the material appearance
        void surf(Input IN, inout SurfaceOutputStandard o) {
            // Sample base texture color
            fixed4 baseColor = tex2D(_MainTex, IN.uv_MainTex);

            // Compute normal from normal map
            float3 normalFromMap = UnpackNormal(tex2D(_NormalMap, IN.uv_NormalMap));
            o.Normal = normalFromMap;

            // Blend albedo color with texture color
            fixed4 blendedAlbedo = lerp(_AlbedoColor, baseColor, _Blend);
            o.Albedo = blendedAlbedo.rgb;

            // Compute emission using emission map and color
            fixed4 emission = tex2D(_EmissionMap, IN.uv_EmissionMap) * _EmissionColor;
            o.Emission = emission.rgb;
        }
        ENDCG
    }

    // Fallback shader for unsupported hardware
    FallBack "Diffuse"
}
