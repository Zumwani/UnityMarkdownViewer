﻿////////////////////////////////////////////////////////////////////////////////

using Markdig.Renderers;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using System;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace MG.MDV
{
    ////////////////////////////////////////////////////////////////////////////////
    /// <see cref="Markdig.Renderers.HtmlRenderer"/>
    /// <see cref="Markdig.Renderers.Normalize.NormalizeRenderer"/>

    public class RendererMarkdown : RendererBase
    {
        public RenderContext Context;
        public float         ViewWidth = 100.0f;


        float           mIndentSize = 20.0f;

        //
        Vector2         mCursor;
        float           mMarginLeft;
        float           mMarginRight;
        float           mLineOrigin;
        float           mLineHeight;

        float           mWordWidth = 0.0f;
        StringBuilder   mWord      = new StringBuilder();
        StringBuilder   mLine      = new StringBuilder();

        GUIContent      mContent   = new GUIContent();
        IActionHandlers mActions   = null;


        public RendererMarkdown( IActionHandlers imageFetcher, RenderContext context )
        {
            Context  = context;
            mActions = imageFetcher;

            ObjectRenderers.Add( new RendererBlockCode() );
            ObjectRenderers.Add( new RendererBlockList() );
            ObjectRenderers.Add( new RendererBlockHeading() );
            ObjectRenderers.Add( new RendererBlockHtml() );
            ObjectRenderers.Add( new RendererBlockParagraph() );
            ObjectRenderers.Add( new RendererBlockQuote() );
            ObjectRenderers.Add( new RendererBlockThematicBreak() );

            ObjectRenderers.Add( new RendererInlineLink() );
            ObjectRenderers.Add( new RendererInlineAutoLink() );
            ObjectRenderers.Add( new RendererInlineCode() );
            ObjectRenderers.Add( new RendererInlineDelimiter() );
            ObjectRenderers.Add( new RendererInlineEmphasis() );
            ObjectRenderers.Add( new RendererInlineLineBreak() );
            ObjectRenderers.Add( new RendererInlineHtml() );
            ObjectRenderers.Add( new RendererInlineHtmlEntity() );
            ObjectRenderers.Add( new RendererInlineLiteral() );
        }


        //------------------------------------------------------------------------------

        internal void Image( string url, string alt, string title )
        {
            Flush(); // TODO: need to "park" current segment until whole line in complete

            // TODO: test relative / project url's
            // TODO: support image resizing?

            var tex = mActions.FetchImage( url );

            if( tex == null )
            {
                if( string.IsNullOrEmpty( alt ) )
                {
                    Print( $"[{url}]" );
                }
                else
                {
                    Print( $"[{alt}]" );
                }

                return;
            }

            mContent.text    = null;
            mContent.image   = tex;
            mContent.tooltip = title ?? alt;

            if( mCursor.x + tex.width > mMarginRight )
            {
                NewLine();
            }

            GUI.Label( new Rect( mCursor.x, mCursor.y, tex.width, tex.height ), mContent );

            mLineHeight = Mathf.Max( mLineHeight, tex.height );
            mCursor.x += tex.width;
        }


        //------------------------------------------------------------------------------

        internal void Print( string text )
        {
            mLineOrigin = mCursor.x;

            for( var i = 0; i < text.Length; i++ )
            {
                if( text[ i ] == '\n' )
                {
                    NewLine();
                }
                else
                {
                    AddCharacter( text[ i ] );
                }
            }

            AddWord();
            Flush(); // TODO: cause an issue with images?
        }

        private void AddCharacter( char ch )
        {
            if( char.IsWhiteSpace( ch ) )
            {
                ch = ' '; // ensure any WS is treated as a space
            }
            
            // TODO: chains of ws chars??

            float advance;

            if( Context.CharacterWidth( ch, out advance ) )
            {
                mWord.Append( ch );
                mWordWidth += advance;
            }
            else
            {
                // bad character
                Context.CharacterWidth( '?', out advance );
                mWord.Append( '?' );
                mWordWidth += advance;
            }

            if( ch == ' ' )
            {
                AddWord();
            }
        }

        private void AddWord()
        {
            if( mWord.Length == 0 )
            {
                return;
            }

            // TODO: split long words?
            // TODO: some safety for narrow windows!

            if( mCursor.x + mWordWidth > mMarginRight )
            {
                NewLine();
            }

            mLine.Append( mWord.ToString() );
            mCursor.x += mWordWidth;

            mWord.Clear();
            mWordWidth = 0.0f;
        }

        private void Flush()
        {
            if( mLine.Length == 0 )
            {
                return;
            }

            mContent.text    = mLine.ToString();
            mContent.image   = null;
            mContent.tooltip = Context.ToolTip;

            var rect = new Rect( mLineOrigin, mCursor.y, mCursor.x - mLineOrigin, Context.Style.lineHeight );

            if( string.IsNullOrWhiteSpace( Context.Link ) )
            {
                GUI.Label( rect, mContent, Context.Style );
            }
            else if( GUI.Button( rect, mContent, Context.Style ) )
            {
                if( Regex.IsMatch( Context.Link, @"^\w+:", RegexOptions.Singleline ) )
                {
                    Application.OpenURL( Context.Link );
                }
                else
                {
                    mActions.SelectPage( Context.Link );
                }
            }

            mLineOrigin = mCursor.x;
            mLine.Clear();
        }


        //------------------------------------------------------------------------------

        internal void HorizontalBreak()
        {
            NewLine();

            var rect = new Rect( mCursor, new Vector2( Screen.width - 50.0f, 1.0f ) );
            GUI.Label( rect, string.Empty, GUI.skin.GetStyle( "hr" ) );

            NewLine();
        }

        public void Prefix( string prefix )
        {
            // TODO: better prefix!
            Print( ( prefix ?? "  " ) + "  " );
        }

        public void Indent()
        {
            // TODO: safety for narrow windows?
            mMarginLeft += mIndentSize;
        }

        public void Outdent()
        {
            mMarginLeft = Mathf.Max( mMarginLeft - mIndentSize, 0.0f );
        }

        private void NewLine()
        {
            Flush();

            mCursor.y += mLineHeight;
            mCursor.x = mMarginLeft;

            mLineOrigin = mCursor.x;
            mLineHeight = Context.Style.lineHeight;
        }

        internal void FinishBlock( bool emptyLine = false )
        {
            NewLine();

            if( emptyLine )
            {
                NewLine();
            }
        }





        ////////////////////////////////////////////////////////////////////////////////
        // utils

        /// <summary>
        /// Output child nodes inline
        /// </summary>
        /// <see cref="Markdig.Renderers.TextRendererBase.WriteLeafInline"/>

        internal void WriteLeafBlockInline( LeafBlock block )
        {
            var inline = block.Inline as Inline;

            while( inline != null )
            {
                Write( inline );
                inline = inline.NextSibling;
            }
        }

        /// <summary>
        /// Output child nodes as raw text
        /// </summary>
        /// <see cref="Markdig.Renderers.HtmlRenderer.WriteLeafRawLines"/>

        internal void WriteLeafRawLines( LeafBlock block )
        {
            if( block.Lines.Lines == null )
            {
                return;
            }

            var lines  = block.Lines;
            var slices = lines.Lines;

            for( int i = 0; i < lines.Count; i++ )
            {
                Print( slices[ i ].ToString() + "\n" );
            }
        }

        internal string GetContents( ContainerInline node )
        {
            if( node == null )
            {
                return string.Empty;
            }

            /// <see cref="Markdig.Renderers.RendererBase.WriteChildren(ContainerInline)"/>
            
            var inline  = node.FirstChild;
            var content = string.Empty;

            while( inline != null )
            {
                var lit = inline as LiteralInline;

                if( lit != null )
                {
                    content += lit.Content.ToString();
                }

                inline = inline.NextSibling;
            }

            return content;
        }


        ////////////////////////////////////////////////////////////////////////////////
        // setup

        public override object Render( MarkdownObject document )
        {
            mCursor      = Vector2.zero;
            mLineOrigin  = 0.0f;
            mMarginLeft  = 0.0f;
            mMarginRight = ViewWidth;

            Context.Reset();

            Write( document );
            FinishBlock();

            return this;
        }
    }
}
