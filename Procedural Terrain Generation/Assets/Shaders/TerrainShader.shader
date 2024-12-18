Shader "Custom/TerrainShader" {
    Properties {
        // Texture Properties
        _MainTex ("Base Texture", 2D) = "white" {}
        _NormalMap ("Normal Map", 2D) = "bump" {}

        // Blend Factor
        _TextureBlend ("Texture Blend Factor", Range(0, 1)) = 0.5
    }

    SubShader {
        Tags { "RenderType" = "Opaque" }
        LOD 200

        CGPROGRAM
        // Define the surface shader model
        #pragma surface surf Standard fullforwardshadows
        #pragma target 3.0

        // Texture samplers
        sampler2D _MainTex;
        sampler2D _NormalMap;

        // Blend factor
        float _TextureBlend;

        // Input structure for surface shader
        struct Input {
            float2 uv_MainTex;      // UV coordinates for base texture
            float2 uv_NormalMap;    // UV coordinates for normal map
            float4 color : COLOR;   // Vertex color
        };

        // Surface function to define the material's properties
        void surf(Input IN, inout SurfaceOutputStandard o) {
            // Sample base texture
            float4 baseTexture = tex2D(_MainTex, IN.uv_MainTex);

            // Set alpha from texture
            o.Alpha = baseTexture.a;

            // Compute normal from normal map
            float3 normalFromMap = UnpackNormal(tex2D(_NormalMap, IN.uv_NormalMap));
            o.Normal = normalFromMap;

            // Blend vertex color with texture color
            float3 blendedAlbedo = lerp(IN.color.rgb, baseTexture.rgb, _TextureBlend);
            o.Albedo = blendedAlbedo;
        }
        ENDCG
    }

    // Fallback for older hardware
    FallBack "Diffuse"
}
