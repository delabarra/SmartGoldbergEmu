using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace SmartGoldbergEmu.Helpers
{
    // Depth32Bit ImageList keeps owned Image refs, but Clear/Remove do not Dispose them.
    public sealed class ImageListOwnedImages : IDisposable
    {
        private readonly Dictionary<string, Image> _owned =
            new Dictionary<string, Image>(StringComparer.OrdinalIgnoreCase);

        public bool ContainsKey(string key)
        {
            return !string.IsNullOrEmpty(key) && _owned.ContainsKey(key);
        }

        public void Set(ImageList imageList, string key, Image image)
        {
            if (imageList == null || string.IsNullOrEmpty(key) || image == null)
            {
                image?.Dispose();
                return;
            }

            Remove(imageList, key);
            imageList.Images.Add(key, image);
            _owned[key] = image;
        }

        // ImageList entry borrows a shared animation frame; previous owned image (if any) is disposed.
        public void SetShared(ImageList imageList, string key, Image image)
        {
            if (imageList == null || string.IsNullOrEmpty(key) || image == null)
                return;

            Remove(imageList, key);
            imageList.Images.Add(key, image);
        }

        public void Remove(ImageList imageList, string key)
        {
            if (string.IsNullOrEmpty(key))
                return;

            if (imageList?.Images != null && imageList.Images.ContainsKey(key))
                imageList.Images.RemoveByKey(key);

            Image image;
            if (_owned.TryGetValue(key, out image))
            {
                _owned.Remove(key);
                image.Dispose();
            }
        }

        public void Clear(ImageList imageList)
        {
            if (imageList?.Images != null)
                imageList.Images.Clear();

            foreach (var image in _owned.Values)
                image.Dispose();
            _owned.Clear();
        }

        // Call before ImageList.Dispose so the list (not this map) owns remaining images.
        public void ReleaseOwnership()
        {
            _owned.Clear();
        }

        public void Dispose()
        {
            foreach (var image in _owned.Values)
                image.Dispose();
            _owned.Clear();
        }
    }
}
