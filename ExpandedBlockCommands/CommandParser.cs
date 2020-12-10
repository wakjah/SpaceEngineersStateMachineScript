using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using SpaceEngineers.Game.ModAPI.Ingame;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using System.Text;
using System;
using VRage.Collections;
using VRage.Game.Components;
using VRage.Game.GUI.TextPanel;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRage.Game.ModAPI.Ingame;
using VRage.Game.ObjectBuilders.Definitions;
using VRage.Game;
using VRage;
using VRageMath;

namespace IngameScript
{
    partial class Program
    {
        public class CommandParser
        {
            enum Character
            {
                Whitespace,
                Letter,
                Digit,
                Semicolon,
                LParen,
                RParen,
                DecimalPoint,
                Plus,
                Minus,
                Quote,
                Underscore,
                Comma,
                Backslash,
                Unknown,
                Eof,
            }

            struct Token
            {
                public int position;
                public char c;
                public Character id;

                public Token(int position, char c, Character id)
                {
                    this.position = position;
                    this.c = c;
                    this.id = id;
                }
            }

            public struct Command
            {
                public string name;
                public List<string> args;

                public Command(string name, List<string> args)
                {
                    this.name = name;
                    this.args = args;
                }

                public override string ToString()
                {
                    string s = name + ": ";
                    foreach (string arg in args)
                    {
                        s += "'" + arg + "', ";
                    }
                    return s;
                }
            }

            string _input;
            int _position;
            Program _program;

            bool eof()
            {
                return _position >= _input.Length;
            }

            Token peek(int lookahead = 0)
            {
                if ((_position + lookahead) >= _input.Length)
                {
                    return new Token(_position + lookahead, (char) 0, Character.Eof);
                }

                char c = _input[_position + lookahead];
                return new Token(_position + lookahead, c, lex(c));
            }

            void consume()
            {
                if (_position + 1 > _input.Length)
                {
                    throw new Exception("Unexpected EOF");
                }
                ++_position;
            }

            Token next()
            {
                Token nextc = peek();
                if (nextc.id == Character.Eof)
                {
                    throw new Exception("Unexpected EOF");
                }
                consume();
                return nextc;
            }

            char expect(Character c)
            {
                Token tok = peek();
                if (tok.id != c)
                {
                    throw new Exception("Unexpected " + tok.id + "; expected " + c + " (at position " + tok.position + ")");
                }
                consume();
                return tok.c;
            }

            Character lex(char c)
            {
                if (char.IsWhiteSpace(c))
                {
                    return Character.Whitespace;
                }

                if (char.IsDigit(c))
                {
                    return Character.Digit;
                }

                if (char.IsLetter(c))
                {
                    return Character.Letter;
                }

                switch (c)
                {
                    case ';': return Character.Semicolon;
                    case '(': return Character.LParen;
                    case ')': return Character.RParen;
                    case '.': return Character.DecimalPoint;
                    case '+': return Character.Plus;
                    case '-': return Character.Minus;
                    case '"': return Character.Quote;
                    case '_': return Character.Underscore;
                    case ',': return Character.Comma;
                    case '\\': return Character.Backslash;
                }

                return Character.Unknown;
            }

            public List<Command> parse(string s, Program program)
            {
                _program = program;
                _input = s;
                _position = 0;
                List<Command> commands = commandSequence();
                _input = null;
                return commands;
            }

            private List<Command> commandSequence()
            {
                List<Command> commands = new List<Command>();

                while (!eof())
                {
                    consumeWhitespace();
                    if (eof())
                    {
                        break;
                    }

                    Command? cmd = command();
                    if (cmd != null)
                    {
                        commands.Add((Command)cmd);
                    }
                }

                return commands;
            }

            private void consumeWhitespace()
            {
                while (peek().id == Character.Whitespace)
                {
                    consume();
                }
            }

            private Command command()
            {
                string name = identifier();
                consumeWhitespace();
                expect(Character.LParen);
                List<string> args = new List<string>();
                while (true)
                {
                    string arg = argument();
                    if (arg == null)
                    {
                        break;
                    }
                    args.Add(arg);
                }
                expect(Character.RParen);

                consumeWhitespace();

                expect(Character.Semicolon);

                consumeWhitespace();

                return new Command(name, args);
            }

            private string identifier()
            {
                int start = _position;
                int end = start;
                while (true)
                {
                    Token c = peek();
                    if (c.id == Character.Letter || c.id == Character.Digit || c.id == Character.Underscore)
                    {
                        ++end;
                        consume();
                    }
                    else
                    {
                        break;
                    }
                }

                if (start == end)
                {
                    throw new Exception("Expected: identifier");
                }

                return _input.Substring(start, end - start);
            }

            private string argument()
            {
                consumeWhitespace();

                string arg;

                {
                    Token tok = peek();
                    if (tok.id == Character.RParen)
                    {
                        return null;
                    }
                    else if (tok.id == Character.Quote)
                    {
                        arg = stringValue();
                    }
                    else
                    {
                        arg = unknownTypeArgument();
                    }
                }

                consumeWhitespace();

                {
                    Token tok = peek();
                    if (tok.id == Character.Comma)
                    {
                        consume();
                    }
                    else if (tok.id == Character.RParen)
                    {
                        // ok
                    }
                    else
                    {
                        throw new Exception("Unexpected character " + tok.id + " (at position " + tok.position + ")");
                    }
                }

                return arg;
            }

            private string stringValue()
            {
                expect(Character.Quote);

                string arg = "";

                bool escape = false;
                while (true)
                {
                    Token tok = peek();
                    if (escape)
                    {
                        if (tok.id == Character.Backslash)
                        {
                            arg += '\\';
                        }
                        else if (tok.id == Character.Quote)
                        {
                            arg += '"';
                        }
                        else if (tok.c == 'n')
                        {
                            arg += '\n';
                        }
                        else
                        {
                            throw new Exception("Unexpected escape sequence (at position " + tok.position + ")");
                        }

                        consume();

                        escape = false;
                    }
                    else
                    {
                        if (tok.id == Character.Eof)
                        {
                            throw new Exception("Unexpected EOF while parsing string argument");
                        }

                        if (tok.id == Character.Quote)
                        {
                            break;
                        }

                        if (tok.id == Character.Backslash)
                        {
                            escape = true;
                            consume();
                            continue;
                        }

                        arg += tok.c;
                        consume();
                    }
                }

                expect(Character.Quote);

                return arg;
            }

            private string unknownTypeArgument()
            {
                string arg = "";

                while (true)
                {
                    Token tok = peek();

                    switch (tok.id)
                    {
                        case Character.Eof:
                            throw new Exception("Unexpected EOF while parsing argument");
                        case Character.Comma:
                        case Character.RParen:
                            if (arg.Length == 0)
                            {
                                throw new Exception("Missing argument (at position " + tok.position + ")");
                            }
                            return arg;
                        case Character.Letter:
                        case Character.DecimalPoint:
                        case Character.Digit:
                        case Character.Minus:
                        case Character.Plus:
                            arg += tok.c;
                            consume();
                            break;
                        default:
                            throw new Exception("Unexpected " + tok.c + " (at position " + tok.position + ")");
                    }
                }
            }
        }
    }
}
