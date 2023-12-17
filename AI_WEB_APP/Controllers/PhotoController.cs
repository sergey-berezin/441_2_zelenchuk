using Microsoft.AspNetCore.Mvc;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp;
using AIPack;
using SixLabors.ImageSharp.Formats.Jpeg;
using System.Text.Json;
using Microsoft.AspNetCore.Components.Server;
using System.IO;

namespace AI_WEB_APP.Controllers {
    
    [ApiController]
    [Route("[controller]")]
    public class PhotoController : ControllerBase {

        private readonly ILogger<Photo> _logger;
        private CancellationTokenSource CTS { get; set; }
        private AIManager AiManager { get; set; }

        public PhotoController(ILogger<Photo> loger) {
            CTS = new CancellationTokenSource();
            AiManager = new AIManager();
            _logger = loger;
            try {
                AiManager.DownloadModel();
            }
            catch (Exception ex) {
                _logger.LogCritical(ex.Message);
            }
        }

        [HttpPost]
        public async Task<ActionResult<List<Photo>>> Post([FromBody] ReciveData data) {
            List<Photo> photoList = new List<Photo>();
            Image<Rgb24> sourceImage;

            data.img = "";

            try {
                using (MemoryStream memoryStream = new MemoryStream(Convert.FromBase64String(data.img))) {
                    sourceImage = Image.Load<Rgb24>(memoryStream);
                }
            }
            catch(Exception ex) {
                _logger.LogCritical(ex.Message);
                return BadRequest(ex.Message);
            }

            if (CTS.IsCancellationRequested) {
                CTS = new CancellationTokenSource();
            }
            try {
                var task = await AiManager.CallModelAsync(sourceImage, CTS.Token);

                if (task.ObjectCount == 0) {
                    using (MemoryStream memoryStream = new MemoryStream()) {
                        var res_img = Image.Load<Rgb24>(Directory.GetCurrentDirectory() + "\\ures_side\\empty.jpg");
                        res_img.Save(memoryStream, new JpegEncoder());
                        photoList.Add(new Photo() { Class = "None", Img = Convert.ToBase64String(memoryStream.ToArray()), Id = data.id, Сonfidence = 0 });
                        return Ok(photoList);
                    }
                }

                foreach(var res in task.ResultForWeb) {
                    using (MemoryStream memoryStream = new MemoryStream()) {
                        res.Img.Save(memoryStream, new JpegEncoder());
                        photoList.Add(new Photo() { Class = res.Class, Img = Convert.ToBase64String(memoryStream.ToArray()), Id = data.id, Сonfidence = res.Confidence });
                    }
                }
            }
            catch(Exception ex) {
                _logger.LogCritical(ex.Message);
                return Problem(ex.Message);
            }

            //Thread.Sleep(2000);
            return Ok(photoList);
        }
    }
    public class ReciveData {
        public int id { get; set; }
        public string img { get; set; }
    }
}
