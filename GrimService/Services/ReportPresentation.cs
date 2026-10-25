using System;
using System.Collections.Generic;
using System.Text.Json;

namespace GrimService.Services
{

    public sealed class ReportPresentation
    {
        public string Kind { get; private set; } = "Nieznany rodzaj treści";
        public string Code { get; private set; } = "";
        public string Reason { get; private set; } = "";
        public string Evidence { get; private set; } = "";
        public string Comment { get; private set; } = "";
        public Dictionary<string,string> References { get; } = new();
        public static ReportPresentation Parse(string payload)
        {
            var result = new ReportPresentation();
            try
            {
                var fields = JsonSerializer.Deserialize<Dictionary<string,string>>(payload);
                string Get(string key) => fields != null && fields.TryGetValue(key, out var value) ? value ?? "" : "";
                result.Code=Get("what"); result.Reason=Get("type");
                result.Evidence=Get("value1"); result.Comment=Get("comment");
                result.Kind=result.Code switch {
                    "UN"=>"Nazwa gracza", "UC"=>"Opis profilu", "UR"=>"Status gracza", "UP"=>"Czas gry",
                    "UPH"=>"Zdjęcie profilowe", "UPPH" or "UPPHP"=>"Zdjęcie gracza", "UPPC"=>"Opis zdjęcia",
                    "CT"=>"Nazwa klubu", "CD"=>"Opis klubu", "CR"=>"Aktualność klubu", "CPH"=>"Zdjęcie klubu",
                    "CET"=>"Nazwa eventu", "CED"=>"Opis eventu", "CEPH"=>"Zdjęcie eventu",
                    "BTT"=>"Tytuł wątku forum", "BTC"=>"Treść wątku forum", "BC"=>"Komentarz na forum",
                    "BCPH"=>"Zdjęcie komentarza", "CERT"=>"Wynik eventu", "MC"=>"Wiadomość prywatna",
                    "CST"=>"Nazwa trasy", "CSC"=>"Opis trasy", "CSPH"=>"Zdjęcie trasy",
                    _=>"Nieznany rodzaj treści"
                };
                var labels = result.Code switch {
                    "CT" or "CD" or "CR" => new[]{"ID klubu"}, "CPH"=>new[]{"ID klubu","ID zdjęcia"},
                    "CET" or "CED"=>new[]{"ID klubu","ID eventu"}, "CEPH"=>new[]{"ID klubu","ID eventu","ID zdjęcia"},
                    "BTT" or "BTC"=>new[]{"ID klubu","ID wątku"}, "BC"=>new[]{"ID klubu","ID wątku","ID komentarza"},
                    "BCPH"=>new[]{"ID klubu","ID wątku","ID komentarza","ID zdjęcia"},
                    "CERT"=>new[]{"ID klubu","ID eventu","ID wyniku"}, "MC"=>new[]{"ID wiadomości"},
                    "CST" or "CSC"=>new[]{"ID trasy"}, "CSPH"=>new[]{"ID trasy","ID zdjęcia"},
                    "UPH" or "UPPH" or "UPPHP" or "UPPC"=>new[]{"PSN ID","ID zdjęcia"},
                    "UN" or "UC" or "UR" or "UP"=>new[]{"PSN ID"}, _=>Array.Empty<string>()
                };
                var keys=Get("key").Split(',');
                for (var i=0;i<labels.Length && i<keys.Length;i++) result.References[labels[i]]=keys[i];
            }
            catch (JsonException) { }
            return result;
        }
    }
}
