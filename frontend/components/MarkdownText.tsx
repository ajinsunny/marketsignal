'use client';

import React from 'react';

/**
 * Simple Markdown Renderer
 *
 * Renders markdown-style text with proper formatting:
 * - **bold text**
 * - Paragraphs
 * - Line breaks
 */

interface MarkdownTextProps {
  text: string;
  className?: string;
}

export default function MarkdownText({ text, className = '' }: MarkdownTextProps) {
  // Split text into paragraphs
  const paragraphs = text.split('\n\n').filter(p => p.trim());

  const renderInlineMarkdown = (line: string) => {
    const parts: (string | React.ReactElement)[] = [];
    let lastIndex = 0;

    // Match **bold** text
    const boldRegex = /\*\*([^*]+)\*\*/g;
    let match;

    while ((match = boldRegex.exec(line)) !== null) {
      // Add text before the bold part
      if (match.index > lastIndex) {
        parts.push(line.substring(lastIndex, match.index));
      }

      // Add bold text
      parts.push(
        <strong key={match.index} className="font-semibold">
          {match[1]}
        </strong>
      );

      lastIndex = match.index + match[0].length;
    }

    // Add remaining text
    if (lastIndex < line.length) {
      parts.push(line.substring(lastIndex));
    }

    return parts.length > 0 ? parts : line;
  };

  return (
    <div className={className}>
      {paragraphs.map((paragraph, idx) => (
        <p key={idx} className={idx > 0 ? 'mt-3' : ''}>
          {renderInlineMarkdown(paragraph)}
        </p>
      ))}
    </div>
  );
}
