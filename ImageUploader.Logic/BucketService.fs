namespace ImageUploader.Logic

open Amazon.S3
open Amazon.S3.Model
open System.IO
open System.Xml.Linq
open System.Threading.Tasks

module BucketService =

    [<Literal>]
    let BucketName = "my-fsharp-image-gallery"

    [<Literal>]
    let BucketUrl = "https://my-fsharp-image-gallery.s3.amazonaws.com"

    [<Literal>]
    let xmlKey = "index2.xml"

    let uploadImage (s3: #IAmazonS3) (fileName: string) (content: Stream) =
        let request =
            PutObjectRequest(BucketName = BucketName, Key = $"images/{fileName}", InputStream = content)

        s3.PutObjectAsync(request)

    let updateXmlMetadata (s3: #IAmazonS3) (newFileName: string) =
        task {


            let! (xmlDoc: XDocument) =
                task {
                    try
                        let! response = s3.GetObjectAsync(BucketName, xmlKey)
                        use reader = new StreamReader(response.ResponseStream)
                        return XDocument.Parse(reader.ReadToEnd())
                    with _ ->
                        return XDocument(XElement(XName.Get "Files"))
                }

            xmlDoc.Root.Add(
                XElement(
                    XName.Get "File",
                    XElement(XName.Get "Name", newFileName),
                    XElement(XName.Get "Link", $"{BucketUrl}/images/{newFileName}"),
                    XElement(XName.Get "UploadDate", System.DateTime.UtcNow.ToString("o")) // Example of adding a third info
                )
            )


            use ms = new MemoryStream()
            xmlDoc.Save(ms)
            ms.Position <- 0L

            let putReq =
                PutObjectRequest(BucketName = BucketName, Key = xmlKey, InputStream = ms)

            // Final line is the expression result
            let! response = s3.PutObjectAsync(putReq)
            return response
        }

    let deleteImage (s3: #IAmazonS3) (fileName: string) =
        task {
            let request =
                DeleteObjectRequest(BucketName = BucketName, Key = $"images/{fileName}")


            let! _ = s3.DeleteObjectAsync(request)

            try
                let! response = s3.GetObjectAsync(BucketName, xmlKey)
                use reader = new StreamReader(response.ResponseStream)
                let xmlDoc = XDocument.Parse(reader.ReadToEnd())

                let targetElement =


                    xmlDoc.Descendants(XName.Get "File")
                    |> Seq.tryFind (fun el ->
                        let nameTag = el.Element(XName.Get "Name")
                        nameTag <> null && (nameTag.Value = fileName || nameTag.Value = System.Net.WebUtility.UrlDecode(fileName)))

                match targetElement with
                | Some el ->
                    el.Remove() // Remove from XML tree

                    // Save updated XML back to S3
                    use ms = new MemoryStream()
                    xmlDoc.Save(ms)
                    ms.Position <- 0L

                    let putReq =
                        PutObjectRequest(BucketName = BucketName, Key = xmlKey, InputStream = ms)

                    let! _ = s3.PutObjectAsync(putReq)
                    return true
                | None -> return false
            with _ ->
                return false

        }
