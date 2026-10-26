using System;
using System.IO;
using System.Text;
using System.Web;
using Sitecore.Diagnostics;

namespace Sitecore.MarkdownForAI.WebForms
{
    /// <summary>
    /// Response filter for WebForms pages: buffers the rendered HTML and, when the response is
    /// closed, writes the Markdown produced by <c>transform</c> instead. Error responses and
    /// failed conversions are passed through unchanged.
    /// </summary>
    internal sealed class MarkdownResponseFilter : Stream
    {
        private readonly Stream _inner;
        private readonly HttpResponse _response;
        private readonly Func<string, string> _transform;
        private readonly MemoryStream _buffer = new MemoryStream();
        private bool _closed;

        public MarkdownResponseFilter(Stream inner, HttpResponse response, Func<string, string> transform)
        {
            _inner = inner;
            _response = response;
            _transform = transform;
        }

        public override bool CanRead
        {
            get { return false; }
        }

        public override bool CanSeek
        {
            get { return false; }
        }

        public override bool CanWrite
        {
            get { return true; }
        }

        public override long Length
        {
            get { throw new NotSupportedException(); }
        }

        public override long Position
        {
            get { throw new NotSupportedException(); }
            set { throw new NotSupportedException(); }
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            _buffer.Write(buffer, offset, count);
        }

        /// <summary>Intentionally empty: the whole page is needed before it can be converted.</summary>
        public override void Flush()
        {
        }

        public override void Close()
        {
            if (_closed)
            {
                return;
            }

            _closed = true;
            byte[] output = Transform();
            _inner.Write(output, 0, output.Length);
            _inner.Flush();
            _inner.Close();
            base.Close();
        }

        private byte[] Transform()
        {
            byte[] raw = _buffer.ToArray();
            if (_response.StatusCode != 200 || raw.Length == 0)
            {
                return raw;
            }

            try
            {
                return Encoding.UTF8.GetBytes(_transform(Encoding.UTF8.GetString(raw)));
            }
            catch (Exception ex)
            {
                Log.Error("MarkdownForAI: failed to convert a WebForms page to Markdown", ex, this);
                return raw;
            }
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }
    }
}
