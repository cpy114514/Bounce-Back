using UnityEngine;

namespace BounceBack.GeometryWorld.Editor
{
    internal static class MenuArtPalette
    {
        internal enum District { Unchanged, Santorini, ClockTower, Pavilion, Habitat, Title }

        internal static Color32 ColorFor(string source, Color32 fallback, District district)
        {
            return district switch
            {
                District.Santorini => source switch
                {
                    "3D616E" => new(65, 101, 122, 255),
                    "47859C" => new(52, 112, 143, 255),
                    "5A9BB4" or "5E9FB8" or "61A3BD" => new(68, 133, 161, 255),
                    "8FBFBD" => new(145, 187, 183, 255),
                    "B28C59" => new(170, 137, 96, 255),
                    "BFB8A1" => new(214, 189, 160, 255),
                    "F5EBD4" or "FFF7E3" => new(234, 224, 203, 255),
                    _ => fallback
                },
                District.ClockTower => source switch
                {
                    "374651" or "414E58" or "45484F" or "4B4E58" or "4B5864" => new(82, 91, 116, 255),
                    "536A79" or "688392" or "728B96" or "7995A4" => new(100, 123, 135, 255),
                    "5A5860" or "5F5B65" => new(124, 119, 147, 255),
                    "AF8E61" or "BC9975" or "BD9053" => new(170, 142, 110, 255),
                    "AF9A85" or "B39B80" => new(167, 164, 190, 255),
                    "CDB99F" or "D3B792" or "DDC6A5" => new(202, 175, 144, 255),
                    "F0DCBB" or "F9EFDB" => new(241, 229, 208, 255),
                    _ => fallback
                },
                District.Pavilion => source switch
                {
                    "56887B" => new(69, 110, 95, 255),
                    "609282" or "9CB594" => new(113, 148, 126, 255),
                    "704538" => new(92, 77, 79, 255),
                    "9C4E41" or "B86D53" => new(156, 86, 70, 255),
                    "B29870" or "D4AC66" => new(176, 149, 110, 255),
                    "CAB9A0" => new(207, 184, 160, 255),
                    "E3D5BC" => new(240, 227, 206, 255),
                    _ => fallback
                },
                District.Habitat => source switch
                {
                    "E9DCC6" => new(216, 204, 182, 255),
                    "F1E7D6" => new(226, 219, 198, 255),
                    "CCBCA6" => new(181, 166, 144, 255),
                    "78989E" => new(91, 128, 132, 255),
                    _ => fallback
                },
                District.Title => source switch
                {
                    "4F5875" => new(47, 65, 68, 255),
                    "DFB97D" => new(181, 141, 87, 255),
                    "F2DFC6" => new(227, 214, 190, 255),
                    "BB8D8C" => new(153, 121, 112, 255),
                    "89839F" => new(107, 114, 113, 255),
                    _ => fallback
                },
                _ => fallback
            };
        }
    }
}
