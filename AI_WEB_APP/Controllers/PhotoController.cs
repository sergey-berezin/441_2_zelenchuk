using Microsoft.AspNetCore.Mvc;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp;
using AIPack;
using SixLabors.ImageSharp.Formats.Jpeg;

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
        public async Task<ActionResult<List<Photo>>> Post([FromBody] string data) {

            List<Photo> photoList = new List<Photo>();
            Image<Rgb24> sourceImage;
            byte[] resultImage;

            //Convert.ToBase64String()
            
            using (MemoryStream memoryStream = new MemoryStream(Convert.FromBase64String(data))) {
                sourceImage = Image.Load<Rgb24>(memoryStream);
            }

            if (CTS.IsCancellationRequested) {
                CTS = new CancellationTokenSource();
            }
            var task = await AiManager.CallModelAsync(sourceImage, CTS.Token);

            using (MemoryStream memoryStream = new MemoryStream()) {
                task.ResultImage.Save(memoryStream, new JpegEncoder());
                resultImage = memoryStream.ToArray();
            }

            return Ok(new Photo { Class = "test", Data = resultImage, Id = 1, Сonfidence = 0.2f });
        }
    }
}
